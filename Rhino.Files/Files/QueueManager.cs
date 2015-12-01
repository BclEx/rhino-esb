using Common.Logging;
using Rhino.Files.Exceptions;
using Rhino.Files.Internal;
using Rhino.Files.Model;
using Rhino.Files.Monitoring;
using Rhino.Files.Protocol;
using Rhino.Files.Storage;
using Rhino.Files.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Transactions;

#pragma warning disable 420
namespace Rhino.Files
{
    public interface IQueueManager : IDisposable
    {
        event Action<string> FailedToSendMessagesTo;
        event Action<object, MessageEventArgs> MessageQueuedForReceive;
        event Action<object, MessageEventArgs> MessageQueuedForSend;
        event Action<object, MessageEventArgs> MessageReceived;
        event Action<object, MessageEventArgs> MessageSent;

        QueueManagerConfiguration Configuration { get; set; }
        string Endpoint { get; }
        string Path { get; }
        string[] Queues { get; }

        void CreateQueues(params string[] queueNames);
        void DisposeRudely();
        void EnablePerformanceCounters();
        void EnqueueDirectlyTo(string queue, string subqueue, MessagePayload payload);
        void FailedToSendTo(string endpointThatWeFailedToSendTo);
        PersistentMessage[] GetAllMessages(string queueName, string subqueue);
        HistoryMessage[] GetAllProcessedMessages(string queueName);
        PersistentMessageToSend[] GetAllSentMessages();
        PersistentMessageToSend[] GetMessagesCurrentlySending();
        int GetNumberOfMessages(string queueName);
        IQueue GetQueue(string queue);
        string[] GetSubqueues(string queueName);
        void MoveTo(string subqueue, Message message);
        void OnMessageSent(MessageEventArgs messageEventArgs);
        Message Peek(string queueName);
        Message Peek(string queueName, string subqueue);
        Message Peek(string queueName, TimeSpan timeout);
        Message Peek(string queueName, string subqueue, TimeSpan timeout);
        PersistentMessage PeekById(string queueName, MessageId id);
        Message Receive(string queueName);
        Message Receive(string queueName, string subqueue);
        Message Receive(string queueName, TimeSpan timeout);
        Message Receive(string queueName, string subqueue, TimeSpan timeout);
        MessageId Send(Uri uri, MessagePayload payload);
        void WaitForAllMessagesToBeSent();
    }
    public class QueueManager : IQueueManager
    {
        [ThreadStatic]
        static TransactionEnlistment Enlistment;

        [ThreadStatic]
        static Transaction CurrentlyEnslistedTransaction;

        volatile bool _wasStarted;
        volatile bool _wasDisposed;
        volatile int _currentlyInCriticalReceiveStatus;
        volatile int _currentlyInsideTransaction;
        readonly string _endpoint;
        readonly object _newMessageArrivedLock = new object();
        readonly string _path;
        Timer _purgeOldDataTimer;
        readonly QueueStorage _queueStorage;

        Receiver _receiver;
        Thread _sendingThread;
        QueuedMessagesSender _queuedMessagesSender;
        readonly ILog _logger = LogManager.GetLogger(typeof(QueueManager));
        PerformanceMonitor _monitor;
        volatile bool _waitingForAllMessagesToBeSent;

        readonly ThreadSafeSet<MessageId> _receivedMsgs = new ThreadSafeSet<MessageId>();
        bool _disposing;

        public QueueManagerConfiguration Configuration { get; set; }

        public int CurrentlySendingCount
        {
            get { return _queuedMessagesSender.CurrentlySendingCount; }
        }

        public int CurrentlyConnectingCount
        {
            get { return _queuedMessagesSender.CurrentlyConnectingCount; }
        }

        public event Action<string> FailedToSendMessagesTo;
        public event Action<object, MessageEventArgs> MessageQueuedForSend;
        public event Action<object, MessageEventArgs> MessageSent;
        public event Action<object, MessageEventArgs> MessageQueuedForReceive;
        public event Action<object, MessageEventArgs> MessageReceived;

        public QueueManager(string endpoint, string path)
            : this(endpoint, path, new QueueManagerConfiguration()) { }
        public QueueManager(string endpoint, string path, QueueManagerConfiguration configuration)
        {
            Configuration = configuration;
            _endpoint = endpoint;
            _path = path;
            _queueStorage = new QueueStorage(path, configuration);
            _queueStorage.Initialize();
            _queueStorage.Global(actions =>
            {
                _receivedMsgs.Add(actions.GetAlreadyReceivedMessageIds());
                actions.Commit();
            });
            HandleRecovery();
        }

        public void Start()
        {
            AssertNotDisposedOrDisposing();
            if (_wasStarted)
                throw new InvalidOperationException("The Start method may not be invoked more than once.");
            if (_endpoint != null)
            {
                _receiver = new Receiver(_endpoint, AcceptMessages);
                _receiver.Start();
            }
            _queuedMessagesSender = new QueuedMessagesSender(_queueStorage, this);
            _sendingThread = new Thread(_queuedMessagesSender.Send)
            {
                IsBackground = true,
                Name = "Rhino File Sender Thread for " + _path
            };
            _sendingThread.Start();
            _purgeOldDataTimer = new Timer(_ => PurgeOldData(), null, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(3));
            _wasStarted = true;
        }

        public void PurgeOldData()
        {
            _logger.Info("Starting to purge old data");
            try
            {
                PurgeProcessedMessages();
                PurgeOutgoingHistory();
                PurgeOldestReceivedMessageIds();
            }
            catch (Exception e) { _logger.Warn("Failed to purge old data from the system", e); }
        }

        private void PurgeProcessedMessages()
        {
            if (Configuration.EnableProcessedMessageHistory)
                foreach (string queue in Queues)
                    PurgeProcessedMessagesInQueue(queue);
        }

        private void PurgeProcessedMessagesInQueue(string queue)
        {
            // To make this batchable:
            // 1: Move to the end of the history (to the newest messages) and seek 
            //    backword by NumberOfMessagesToKeepInProcessedHistory.
            // 2: Save a bookmark of the current position.
            // 3: Delete from the beginning of the table (oldest messages) in batches until 
            //    a) we reach the bookmark or b) we hit OldestMessageInProcessedHistory.
            MessageBookmark purgeLimit = null;
            var numberOfMessagesToKeep = Configuration.NumberOfMessagesToKeepInProcessedHistory;
            if (numberOfMessagesToKeep > 0)
            {
                _queueStorage.Global(actions =>
                {
                    var queueActions = actions.GetQueue(queue);
                    purgeLimit = queueActions.GetMessageHistoryBookmarkAtPosition(numberOfMessagesToKeep);
                    actions.Commit();
                });
                if (purgeLimit == null)
                    return;
            }

            var foundMessages = false;
            do
            {
                foundMessages = false;
                _queueStorage.Global(actions =>
                {
                    var queueActions = actions.GetQueue(queue);
                    var messages = queueActions.GetAllProcessedMessages(batchSize: 250)
                        .TakeWhile(x => (purgeLimit == null || !x.Bookmark.Equals(purgeLimit)) && (DateTime.Now - x.SentAt) > Configuration.OldestMessageInProcessedHistory);
                    foreach (var message in messages)
                    {
                        foundMessages = true;
                        _logger.DebugFormat("Purging message {0} from queue {1}/{2}", message.Id, message.Queue, message.SubQueue);
                        queueActions.DeleteHistoric(message.Bookmark);
                    }
                    actions.Commit();
                });
            } while (foundMessages);
        }

        private void PurgeOutgoingHistory()
        {
            // Outgoing messages are still stored in the history in case the sender 
            // needs to revert, so there will still be messages to purge even when
            // the QueueManagerConfiguration has disabled outgoing history.
            //
            // To make this batchable:
            // 1: Move to the end of the history (to the newest messages) and seek 
            //    backword by NumberOfMessagesToKeepInOutgoingHistory.
            // 2: Save a bookmark of the current position.
            // 3: Delete from the beginning of the table (oldest messages) in batches until 
            //    a) we reach the bookmark or b) we hit OldestMessageInOutgoingHistory.
            MessageBookmark purgeLimit = null;
            int numberOfMessagesToKeep = Configuration.NumberOfMessagesToKeepInOutgoingHistory;
            if (numberOfMessagesToKeep > 0 && Configuration.EnableOutgoingMessageHistory)
            {
                _queueStorage.Global(actions =>
                {
                    purgeLimit = actions.GetSentMessageBookmarkAtPosition(numberOfMessagesToKeep);
                    actions.Commit();
                });
                if (purgeLimit == null)
                    return;
            }

            var foundMessages = false;
            do
            {
                foundMessages = false;
                _queueStorage.Global(actions =>
                {
                    var sentMessages = actions.GetSentMessages(batchSize: 250)
                        .TakeWhile(x => (purgeLimit == null || !x.Bookmark.Equals(purgeLimit)) && (!Configuration.EnableOutgoingMessageHistory || (DateTime.Now - x.SentAt) > Configuration.OldestMessageInOutgoingHistory));
                    foreach (var sentMessage in sentMessages)
                    {
                        foundMessages = true;
                        _logger.DebugFormat("Purging sent message {0} to {1}/{2}/{3}", sentMessage.Id, sentMessage.Endpoint, sentMessage.Queue, sentMessage.SubQueue);
                        actions.DeleteMessageToSendHistoric(sentMessage.Bookmark);
                    }
                    actions.Commit();
                });
            } while (foundMessages);
        }

        private void PurgeOldestReceivedMessageIds()
        {
            int totalCount = 0;
            List<MessageId> deletedMessageIds = null;
            do
            {
                _queueStorage.Global(actions =>
                {
                    deletedMessageIds = actions.DeleteOldestReceivedMessageIds(Configuration.NumberOfReceivedMessageIdsToKeep, numberOfItemsToDelete: 250).ToList();
                    actions.Commit();
                });
                _receivedMsgs.Remove(deletedMessageIds);
                totalCount += deletedMessageIds.Count;
            } while (deletedMessageIds.Count > 0);
            _logger.InfoFormat("Purged {0} message ids", totalCount);
        }

        private void HandleRecovery()
        {
            var recoveryRequired = false;
            _queueStorage.Global(actions =>
            {
                actions.MarkAllOutgoingInFlightMessagesAsReadyToSend();
                actions.MarkAllProcessedMessagesWithTransactionsNotRegisterForRecoveryAsReadyToDeliver();
                foreach (var bytes in actions.GetRecoveryInformation())
                {
                    recoveryRequired = true;
                    TransactionManager.Reenlist(_queueStorage.Id, bytes, new TransactionEnlistment(_queueStorage, () => { }, () => { }));
                }
                actions.Commit();
            });
            if (recoveryRequired)
                TransactionManager.RecoveryComplete(_queueStorage.Id);
        }

        //public ITransactionalScope BeginTransactionalScope()
        //{
        //    return new TransactionalScope(this, new QueueTransaction(_queueStorage, OnTransactionComplete, AssertNotDisposed));
        //}

        public void EnablePerformanceCounters()
        {
            if (_wasStarted)
                throw new InvalidOperationException("Performance counters cannot be enabled after the queue has been started.");
            _monitor = new PerformanceMonitor(this);
        }

        public string Path
        {
            get { return _path; }
        }

        public string Endpoint
        {
            get { return _endpoint; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_wasDisposed)
                return;
            DisposeResourcesWhoseDisposalCannotFail();
            if (_monitor != null)
                _monitor.Dispose();
            _queueStorage.Dispose();
            // only after we finish incoming recieves, and finish processing active transactions can we mark it as disposed
            _wasDisposed = true;
        }

        public void DisposeRudely()
        {
            if (_wasDisposed)
                return;
            DisposeResourcesWhoseDisposalCannotFail();
            _queueStorage.DisposeRudely();
            // only after we finish incoming recieves, and finish processing active transactions can we mark it as disposed
            _wasDisposed = true;
        }

        private void DisposeResourcesWhoseDisposalCannotFail()
        {
            _disposing = true;
            lock (_newMessageArrivedLock)
                Monitor.PulseAll(_newMessageArrivedLock);
            if (_wasStarted)
            {
                _purgeOldDataTimer.Dispose();
                _queuedMessagesSender.Stop();
                _sendingThread.Join();
                if (_receiver != null)
                    _receiver.Dispose();
            }
            while (_currentlyInCriticalReceiveStatus > 0)
            {
                _logger.WarnFormat("Waiting for {0} messages that are currently in critical receive status", _currentlyInCriticalReceiveStatus);
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
            while (_currentlyInsideTransaction > 0)
            {
                _logger.WarnFormat("Waiting for {0} transactions currently running", _currentlyInsideTransaction);
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        #endregion

        private void OnTransactionComplete()
        {
            lock (_newMessageArrivedLock)
                Monitor.PulseAll(_newMessageArrivedLock);
            Interlocked.Decrement(ref _currentlyInsideTransaction);
        }

        private void AssertNotDisposed()
        {
            if (_wasDisposed)
                throw new ObjectDisposedException("QueueManager");
        }

        private void AssertNotDisposedOrDisposing()
        {
            if (_disposing || _wasDisposed)
                throw new ObjectDisposedException("QueueManager");
        }

        public void WaitForAllMessagesToBeSent()
        {
            _waitingForAllMessagesToBeSent = true;
            try
            {
                var hasMessagesToSend = true;
                do
                {
                    _queueStorage.Send(actions =>
                    {
                        hasMessagesToSend = actions.HasMessagesToSend();
                        actions.Commit();
                    });
                    if (hasMessagesToSend)
                        Thread.Sleep(100);
                } while (hasMessagesToSend);
            }
            finally { _waitingForAllMessagesToBeSent = false; }
        }

        public IQueue GetQueue(string queue)
        {
            return new Queue(this, queue);
        }

        public PersistentMessage[] GetAllMessages(string queueName, string subqueue)
        {
            AssertNotDisposedOrDisposing();
            PersistentMessage[] messages = null;
            _queueStorage.Global(actions =>
            {
                messages = actions.GetQueue(queueName).GetAllMessages(subqueue).ToArray();
                actions.Commit();
            });
            return messages;
        }

        public HistoryMessage[] GetAllProcessedMessages(string queueName)
        {
            AssertNotDisposedOrDisposing();
            HistoryMessage[] messages = null;
            _queueStorage.Global(actions =>
            {
                messages = actions.GetQueue(queueName).GetAllProcessedMessages().ToArray();
                actions.Commit();
            });
            return messages;
        }

        public PersistentMessageToSend[] GetAllSentMessages()
        {
            AssertNotDisposedOrDisposing();
            PersistentMessageToSend[] msgs = null;
            _queueStorage.Global(actions =>
            {
                msgs = actions.GetSentMessages().ToArray();
                actions.Commit();
            });
            return msgs;
        }

        public PersistentMessageToSend[] GetMessagesCurrentlySending()
        {
            AssertNotDisposedOrDisposing();
            PersistentMessageToSend[] msgs = null;
            _queueStorage.Send(actions =>
            {
                msgs = actions.GetMessagesToSend().ToArray();
                actions.Commit();
            });
            return msgs;
        }

        public Message Peek(string queueName) { return Peek(queueName, null, TimeSpan.FromDays(1)); }
        public Message Peek(string queueName, TimeSpan timeout) { return Peek(queueName, null, timeout); }
        public Message Peek(string queueName, string subqueue) { return Peek(queueName, subqueue, TimeSpan.FromDays(1)); }
        public Message Peek(string queueName, string subqueue, TimeSpan timeout)
        {
            var remaining = timeout;
            while (true)
            {
                var message = PeekMessageFromQueue(queueName, subqueue);
                if (message != null)
                    return message;

                lock (_newMessageArrivedLock)
                {
                    message = PeekMessageFromQueue(queueName, subqueue);
                    if (message != null)
                        return message;

                    var sp = Stopwatch.StartNew();
                    if (!Monitor.Wait(_newMessageArrivedLock, remaining))
                        throw new TimeoutException("No message arrived in the specified timeframe " + timeout);
                    remaining = Max(TimeSpan.Zero, remaining - sp.Elapsed);
                }
            }
        }

        private static TimeSpan Max(TimeSpan x, TimeSpan y) { return (x >= y ? x : y); }

        private void EnsureEnlistment()
        {
            AssertNotDisposedOrDisposing();
            if (Transaction.Current == null)
                throw new InvalidOperationException("You must use TransactionScope when using Rhino.Files");
            if (CurrentlyEnslistedTransaction == Transaction.Current)
                return;
            // need to change the enlistment
            Interlocked.Increment(ref _currentlyInsideTransaction);
            Enlistment = new TransactionEnlistment(_queueStorage, OnTransactionComplete, AssertNotDisposed);
            CurrentlyEnslistedTransaction = Transaction.Current;
        }

        private PersistentMessage GetMessageFromQueue(string queueName, string subqueue)
        {
            AssertNotDisposedOrDisposing();
            PersistentMessage message = null;
            _queueStorage.Global(actions =>
            {
                message = actions.GetQueue(queueName).Dequeue(subqueue);
                if (message != null)
                    actions.RegisterUpdateToReverse(Enlistment.Id, message.Bookmark, MessageStatus.ReadyToDeliver, subqueue);
                actions.Commit();
            });
            return message;
        }

        private PersistentMessage PeekMessageFromQueue(string queueName, string subqueue)
        {
            AssertNotDisposedOrDisposing();
            PersistentMessage message = null;
            _queueStorage.Global(actions =>
            {
                message = actions.GetQueue(queueName).Peek(subqueue);
                actions.Commit();
            });
            if (message != null) { _logger.DebugFormat("Peeked message with id '{0}' from '{1}/{2}'", message.Id, queueName, subqueue); }
            return message;
        }

        protected virtual IMessageAcceptance AcceptMessages(Message[] msgs)
        {
            var bookmarks = new List<MessageBookmark>();
            _queueStorage.Global(actions =>
            {
                foreach (var msg in _receivedMsgs.Filter(msgs, message => message.Id))
                {
                    var queue = actions.GetQueue(msg.Queue);
                    var bookmark = queue.Enqueue(msg);
                    bookmarks.Add(bookmark);
                }
                actions.Commit();
            });
            return new MessageAcceptance(this, bookmarks, msgs, _queueStorage);
        }

        #region MessageAcceptance

        private class MessageAcceptance : IMessageAcceptance
        {
            readonly IList<MessageBookmark> _bookmarks;
            readonly IEnumerable<Message> _messages;
            readonly QueueManager _parent;
            readonly QueueStorage _queueStorage;

            public MessageAcceptance(QueueManager parent,
                IList<MessageBookmark> bookmarks,
                IEnumerable<Message> messages,
                QueueStorage queueStorage)
            {
                _parent = parent;
                _bookmarks = bookmarks;
                _messages = messages;
                _queueStorage = queueStorage;
                Interlocked.Increment(ref parent._currentlyInCriticalReceiveStatus);
            }

            public void Commit()
            {
                try
                {
                    _parent.AssertNotDisposed();
                    _queueStorage.Global(actions =>
                    {
                        foreach (var bookmark in _bookmarks)
                            actions.GetQueue(bookmark.QueueName).SetMessageStatus(bookmark, MessageStatus.ReadyToDeliver);
                        foreach (var msg in _messages)
                            actions.MarkReceived(msg.Id);
                        actions.Commit();
                    });
                    _parent._receivedMsgs.Add(_messages.Select(m => m.Id));

                    foreach (var msg in _messages)
                        _parent.OnMessageQueuedForReceive(msg);
                    lock (_parent._newMessageArrivedLock)
                        Monitor.PulseAll(_parent._newMessageArrivedLock);
                }
                finally { Interlocked.Decrement(ref _parent._currentlyInCriticalReceiveStatus); }
            }

            public void Abort()
            {
                try
                {
                    _parent.AssertNotDisposed();
                    _queueStorage.Global(actions =>
                    {
                        foreach (var bookmark in _bookmarks)
                            actions.GetQueue(bookmark.QueueName).Delete(bookmark);
                        actions.Commit();
                    });
                }
                finally { Interlocked.Decrement(ref _parent._currentlyInCriticalReceiveStatus); }
            }
        }

        #endregion

        public void CreateQueues(params string[] queueNames)
        {
            AssertNotDisposedOrDisposing();
            _queueStorage.Global(actions =>
            {
                foreach (var queueName in queueNames)
                    actions.CreateQueueIfDoesNotExists(queueName);
                actions.Commit();
            });
        }

        public string[] Queues
        {
            get
            {
                AssertNotDisposedOrDisposing();
                string[] queues = null;
                _queueStorage.Global(actions =>
                {
                    queues = actions.GetAllQueuesNames();
                    actions.Commit();
                });
                return queues;
            }
        }

        public void MoveTo(string subqueue, Message message)
        {
            AssertNotDisposedOrDisposing();
            EnsureEnlistment();
            _queueStorage.Global(actions =>
            {
                var queue = actions.GetQueue(message.Queue);
                var bookmark = queue.MoveTo(subqueue, (PersistentMessage)message);
                actions.RegisterUpdateToReverse(Enlistment.Id, bookmark, MessageStatus.ReadyToDeliver, message.SubQueue);
                actions.Commit();
            });

            if (((PersistentMessage)message).Status == MessageStatus.ReadyToDeliver)
                OnMessageReceived(message);

            var updatedMessage = new Message
            {
                Id = message.Id,
                Data = message.Data,
                Headers = message.Headers,
                Queue = message.Queue,
                SubQueue = subqueue,
                SentAt = message.SentAt
            };
            OnMessageQueuedForReceive(updatedMessage);
        }

        public void EnqueueDirectlyTo(string queue, string subqueue, MessagePayload payload)
        {
            EnsureEnlistment();
            var message = new PersistentMessage
            {
                Data = payload.Data,
                Headers = payload.Headers,
                Id = new MessageId
                {
                    SourceInstanceId = _queueStorage.Id,
                    MessageIdentifier = GuidCombGenerator.Generate()
                },
                Queue = queue,
                SentAt = DateTime.Now,
                SubQueue = subqueue,
                Status = MessageStatus.EnqueueWait
            };

            _queueStorage.Global(actions =>
            {
                var queueActions = actions.GetQueue(queue);
                var bookmark = queueActions.Enqueue(message);
                actions.RegisterUpdateToReverse(Enlistment.Id, bookmark, MessageStatus.EnqueueWait, subqueue);
                actions.Commit();
            });

            OnMessageQueuedForReceive(message);
            lock (_newMessageArrivedLock)
                Monitor.PulseAll(_newMessageArrivedLock);
        }

        public PersistentMessage PeekById(string queueName, MessageId id)
        {
            PersistentMessage message = null;
            _queueStorage.Global(actions =>
            {
                var queue = actions.GetQueue(queueName);
                message = queue.PeekById(id);
                actions.Commit();
            });
            return message;
        }

        public string[] GetSubqueues(string queueName)
        {
            string[] result = null;
            _queueStorage.Global(actions =>
            {
                var queue = actions.GetQueue(queueName);
                result = queue.Subqueues;
                actions.Commit();
            });
            return result;
        }

        public int GetNumberOfMessages(string queueName)
        {
            int numberOfMsgs = 0;
            _queueStorage.Global(actions =>
            {
                numberOfMsgs = actions.GetNumberOfMessages(queueName);
                actions.Commit();
            });
            return numberOfMsgs;
        }

        public void FailedToSendTo(string endpointThatWeFailedToSendTo)
        {
            var action = FailedToSendMessagesTo;
            if (action != null)
                action(endpointThatWeFailedToSendTo);
        }

        public void OnMessageQueuedForSend(MessageEventArgs messageEventArgs)
        {
            var action = MessageQueuedForSend;
            if (action != null) action(this, messageEventArgs);
        }

        public void OnMessageSent(MessageEventArgs messageEventArgs)
        {
            var action = MessageSent;
            if (action != null) action(this, messageEventArgs);
        }

        private void OnMessageQueuedForReceive(Message message) { OnMessageQueuedForReceive(new MessageEventArgs(null, message)); }
        public void OnMessageQueuedForReceive(MessageEventArgs messageEventArgs)
        {
            var action = MessageQueuedForReceive;
            if (action != null) action(this, messageEventArgs);
        }

        private void OnMessageReceived(Message message) { OnMessageReceived(new MessageEventArgs(null, message)); }
        public void OnMessageReceived(MessageEventArgs messageEventArgs)
        {
            var action = MessageReceived;
            if (action != null) action(this, messageEventArgs);
        }

        public Message Receive(string queueName) { return Receive(queueName, null, TimeSpan.FromDays(1)); }
        public Message Receive(string queueName, TimeSpan timeout) { return Receive(queueName, null, timeout); }
        public Message Receive(string queueName, string subqueue) { return Receive(queueName, subqueue, TimeSpan.FromDays(1)); }
        public Message Receive(string queueName, string subqueue, TimeSpan timeout)
        {
            EnsureEnlistment();
            var remaining = timeout;
            while (true)
            {
                var message = GetMessageFromQueue(queueName, subqueue);
                if (message != null)
                {
                    OnMessageReceived(message);
                    return message;
                }
                lock (_newMessageArrivedLock)
                {
                    message = GetMessageFromQueue(queueName, subqueue);
                    if (message != null)
                    {
                        OnMessageReceived(message);
                        return message;
                    }
                    var sp = Stopwatch.StartNew();
                    if (!Monitor.Wait(_newMessageArrivedLock, remaining))
                        throw new TimeoutException("No message arrived in the specified timeframe " + timeout);
                    var newRemaining = remaining - sp.Elapsed;
                    remaining = (newRemaining >= TimeSpan.Zero ? newRemaining : TimeSpan.Zero);
                }
            }
        }

        public MessageId Send(Uri uri, MessagePayload payload)
        {
            if (_waitingForAllMessagesToBeSent)
                throw new CannotSendWhileWaitingForAllMessagesToBeSentException("Currently waiting for all messages to be sent, so we cannot send. You probably have a race condition in your application.");
            EnsureEnlistment();
            var parts = uri.AbsolutePath.Substring(1).Split('/');
            var queue = parts[0];
            string subqueue = null;
            if (parts.Length > 1)
                subqueue = string.Join("/", parts.Skip(1).ToArray());
            var msgId = Guid.Empty;
            var destination = uri.Host;
            _queueStorage.Global(actions =>
            {
                msgId = actions.RegisterToSend(destination, queue, subqueue, payload, Enlistment.Id);
                actions.Commit();
            });
            var messageId = new MessageId
            {
                SourceInstanceId = _queueStorage.Id,
                MessageIdentifier = msgId
            };
            var message = new Message
            {
                Id = messageId,
                Data = payload.Data,
                Headers = payload.Headers,
                Queue = queue,
                SubQueue = subqueue
            };
            OnMessageQueuedForSend(new MessageEventArgs(destination, message));
            return messageId;
        }
    }
}
#pragma warning restore 420