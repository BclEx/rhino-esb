using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Logging;
using Rhino.Files;
using Rhino.ServiceBus.DataStructures;
using Rhino.ServiceBus.Exceptions;
using Rhino.ServiceBus.Impl;
using Rhino.ServiceBus.Internal;
using Rhino.ServiceBus.MessageModules;
using Rhino.ServiceBus.Messages;
using Rhino.ServiceBus.Transport;
using Rhino.HashTables;

namespace Rhino.ServiceBus.RhinoFiles
{
    public class FileSubscriptionStorage : ISubscriptionStorage, IDisposable, IMessageModule
    {
        const string SubscriptionsKey = "subscriptions";

        readonly Hashtable<string, List<WeakReference>> _localInstanceSubscriptions = new Hashtable<string, List<WeakReference>>();

        readonly ILog _logger = LogManager.GetLogger(typeof(FileSubscriptionStorage));

        readonly IMessageSerializer _messageSerializer;
        HashTable _pht;
        readonly IReflection _reflection;

        readonly MultiValueIndexHashtable<Guid, string, Uri, int> _remoteInstanceSubscriptions = new MultiValueIndexHashtable<Guid, string, Uri, int>();

        readonly Hashtable<TypeAndUriKey, IList<int>> _subscriptionMessageIds = new Hashtable<TypeAndUriKey, IList<int>>();

        readonly string _subscriptionPath;
        readonly Hashtable<string, HashSet<Uri>> _subscriptions = new Hashtable<string, HashSet<Uri>>();

        bool _currentlyLoadingPersistentData;

        public FileSubscriptionStorage(
            string subscriptionPath,
            IMessageSerializer messageSerializer,
            IReflection reflection)
        {
            _subscriptionPath = subscriptionPath;
            _messageSerializer = messageSerializer;
            _reflection = reflection;
            _pht = new HashTable(subscriptionPath);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_pht != null)
            {
                _pht.Dispose();
                _pht = null;
            }
        }

        #endregion

        #region IMessageModule Members

        void IMessageModule.Init(ITransport transport, IServiceBus bus)
        {
            transport.AdministrativeMessageArrived += HandleAdministrativeMessage;
        }

        void IMessageModule.Stop(ITransport transport, IServiceBus bus)
        {
            transport.AdministrativeMessageArrived -= HandleAdministrativeMessage;
        }

        #endregion

        #region ISubscriptionStorage Members

        public void Initialize()
        {
            _logger.DebugFormat("Initializing File subscription storage on: {0}", _subscriptionPath);
            _pht.Initialize();
            _pht.Batch(actions =>
            {
                var items = actions.GetItems(new GetItemsRequest
                {
                    Key = SubscriptionsKey
                });
                foreach (var item in items)
                {
                    object[] msgs;
                    try { msgs = _messageSerializer.Deserialize(new MemoryStream(item.Value)); }
                    catch (Exception e) { throw new SubscriptionException("Could not deserialize message from subscription queue", e); }

                    try
                    {
                        _currentlyLoadingPersistentData = true;
                        foreach (var msg in msgs)
                            HandleAdministrativeMessage(new CurrentMessageInformation
                            {
                                AllMessages = msgs,
                                Message = msg,
                            });
                    }
                    catch (Exception e) { throw new SubscriptionException("Failed to process subscription records", e); }
                    finally { _currentlyLoadingPersistentData = false; }
                }
                actions.Commit();
            });
        }

        public IEnumerable<Uri> GetSubscriptionsFor(Type type)
        {
            HashSet<Uri> subscriptionForType = null;
            _subscriptions.Read(reader => reader.TryGetValue(type.FullName, out subscriptionForType));
            var subscriptionsFor = (subscriptionForType ?? new HashSet<Uri>());
            List<Uri> instanceSubscriptions;
            _remoteInstanceSubscriptions.TryGet(type.FullName, out instanceSubscriptions);
            subscriptionsFor.UnionWith(instanceSubscriptions);
            return subscriptionsFor;
        }

        public void RemoveLocalInstanceSubscription(IMessageConsumer consumer)
        {
            var messagesConsumes = _reflection.GetMessagesConsumed(consumer);
            var changed = false;
            var list = new List<WeakReference>();
            _localInstanceSubscriptions.Write(writer =>
            {
                foreach (var type in messagesConsumes)
                {
                    List<WeakReference> value;
                    if (writer.TryGetValue(type.FullName, out value) == false)
                        continue;
                    writer.Remove(type.FullName);
                    list.AddRange(value);
                }
            });
            foreach (var reference in list)
            {
                if (ReferenceEquals(reference.Target, consumer))
                    continue;
                changed = true;
            }
            if (changed)
                RaiseSubscriptionChanged();
        }

        public object[] GetInstanceSubscriptions(Type type)
        {
            List<WeakReference> value = null;
            _localInstanceSubscriptions.Read(reader => reader.TryGetValue(type.FullName, out value));
            if (value == null)
                return new object[0];
            var liveInstances = value
                .Select(x => x.Target)
                .Where(x => x != null)
                .ToArray();
            if (liveInstances.Length != value.Count) //cleanup
                _localInstanceSubscriptions.Write(writer => value.RemoveAll(x => !x.IsAlive));
            return liveInstances;
        }

        public event Action SubscriptionChanged;

        public bool AddSubscription(string type, string endpoint)
        {
            var added = false;
            _subscriptions.Write(writer =>
            {
                HashSet<Uri> subscriptionsForType;
                if (!writer.TryGetValue(type, out subscriptionsForType))
                {
                    subscriptionsForType = new HashSet<Uri>();
                    writer.Add(type, subscriptionsForType);
                }
                var uri = new Uri(endpoint);
                added = subscriptionsForType.Add(uri);
                _logger.InfoFormat("Added subscription for {0} on {1}", type, uri);
            });

            RaiseSubscriptionChanged();
            return added;
        }

        public void RemoveSubscription(string type, string endpoint)
        {
            var uri = new Uri(endpoint);
            RemoveSubscriptionMessageFromPht(type, uri);
            _subscriptions.Write(writer =>
            {
                HashSet<Uri> subscriptionsForType;
                if (!writer.TryGetValue(type, out subscriptionsForType))
                {
                    subscriptionsForType = new HashSet<Uri>();
                    writer.Add(type, subscriptionsForType);
                }
                subscriptionsForType.Remove(uri);
                _logger.InfoFormat("Removed subscription for {0} on {1}", type, endpoint);
            });
            RaiseSubscriptionChanged();
        }

        public void AddLocalInstanceSubscription(IMessageConsumer consumer)
        {
            _localInstanceSubscriptions.Write(writer =>
            {
                foreach (var type in _reflection.GetMessagesConsumed(consumer))
                {
                    List<WeakReference> value;
                    if (!writer.TryGetValue(type.FullName, out value))
                    {
                        value = new List<WeakReference>();
                        writer.Add(type.FullName, value);
                    }
                    value.Add(new WeakReference(consumer));
                }
            });
            RaiseSubscriptionChanged();
        }

        #endregion

        private void AddMessageIdentifierForTracking(int messageId, string messageType, Uri uri)
        {
            _subscriptionMessageIds.Write(writer =>
            {
                var key = new TypeAndUriKey { TypeName = messageType, Uri = uri };
                IList<int> value;
                if (!writer.TryGetValue(key, out value))
                {
                    value = new List<int>();
                    writer.Add(key, value);
                }
                value.Add(messageId);
            });
        }

        private void RemoveSubscriptionMessageFromPht(string type, Uri uri)
        {
            _subscriptionMessageIds.Write(writer =>
            {
                var key = new TypeAndUriKey
                {
                    TypeName = type,
                    Uri = uri
                };
                IList<int> messageIds;
                if (!writer.TryGetValue(key, out messageIds))
                    return;
                _pht.Batch(actions =>
                {
                    foreach (var msgId in messageIds)
                        actions.RemoveItem(new RemoveItemRequest
                        {
                            Id = msgId,
                            Key = SubscriptionsKey
                        });
                    actions.Commit();
                });
                writer.Remove(key);
            });
        }

        public bool HandleAdministrativeMessage(CurrentMessageInformation msgInfo)
        {
            var addSubscription = msgInfo.Message as AddSubscription;
            if (addSubscription != null)
                return ConsumeAddSubscription(addSubscription);
            var removeSubscription = msgInfo.Message as RemoveSubscription;
            if (removeSubscription != null)
                return ConsumeRemoveSubscription(removeSubscription);
            var addInstanceSubscription = msgInfo.Message as AddInstanceSubscription;
            if (addInstanceSubscription != null)
                return ConsumeAddInstanceSubscription(addInstanceSubscription);
            var removeInstanceSubscription = msgInfo.Message as RemoveInstanceSubscription;
            if (removeInstanceSubscription != null)
                return ConsumeRemoveInstanceSubscription(removeInstanceSubscription);
            return false;
        }

        public bool ConsumeRemoveInstanceSubscription(RemoveInstanceSubscription subscription)
        {
            int msgId;
            if (_remoteInstanceSubscriptions.TryRemove(subscription.InstanceSubscriptionKey, out msgId))
            {
                _pht.Batch(actions =>
                {
                    actions.RemoveItem(new RemoveItemRequest
                    {
                        Id = msgId,
                        Key = SubscriptionsKey
                    });
                    actions.Commit();
                });
                RaiseSubscriptionChanged();
            }
            return true;
        }

        public bool ConsumeAddInstanceSubscription(
            AddInstanceSubscription subscription)
        {
            _pht.Batch(actions =>
            {
                var message = new MemoryStream();
                _messageSerializer.Serialize(new[] { subscription }, message);
                var itemId = actions.AddItem(new AddItemRequest
                {
                    Key = SubscriptionsKey,
                    Data = message.ToArray()
                });
                _remoteInstanceSubscriptions.Add(subscription.InstanceSubscriptionKey, subscription.Type, new Uri(subscription.Endpoint), itemId);
                actions.Commit();
            });
            RaiseSubscriptionChanged();
            return true;
        }

        public bool ConsumeRemoveSubscription(RemoveSubscription removeSubscription)
        {
            RemoveSubscription(removeSubscription.Type, removeSubscription.Endpoint.Uri.ToString());
            return true;
        }

        public bool ConsumeAddSubscription(AddSubscription addSubscription)
        {
            var newSubscription = AddSubscription(addSubscription.Type, addSubscription.Endpoint.Uri.ToString());
            if (newSubscription && !_currentlyLoadingPersistentData)
            {
                var itemId = 0;
                _pht.Batch(actions =>
                {
                    var stream = new MemoryStream();
                    _messageSerializer.Serialize(new[] { addSubscription }, stream);
                    itemId = actions.AddItem(new AddItemRequest
                    {
                        Key = SubscriptionsKey,
                        Data = stream.ToArray()
                    });
                    actions.Commit();
                });
                AddMessageIdentifierForTracking(itemId, addSubscription.Type, addSubscription.Endpoint.Uri);
                return true;
            }
            return false;
        }

        private void RaiseSubscriptionChanged()
        {
            var copy = SubscriptionChanged;
            if (copy != null)
                copy();
        }
    }
}