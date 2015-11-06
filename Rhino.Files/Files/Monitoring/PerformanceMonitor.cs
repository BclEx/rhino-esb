using Common.Logging;
using Rhino.Files.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;

namespace Rhino.Files.Monitoring
{
    public class PerformanceMonitor : IDisposable
    {
        readonly IPerformanceCountersProvider _immediatelyRecordingProvider = new ImmediatelyRecordingCountersProvider();
        readonly ILog _logger = LogManager.GetLogger(typeof(PerformanceMonitor));
        readonly IQueueManager _queueManager;
        readonly Dictionary<Transaction, IPerformanceCountersProvider> _transactionalProviders = new Dictionary<Transaction, IPerformanceCountersProvider>();

        public PerformanceMonitor(IQueueManager queueManager)
        {
            _queueManager = queueManager;
            AssertCountersExist();
            AttachToEvents();
            SyncWithCurrentQueueState();
        }

        protected virtual void AssertCountersExist()
        {
            OutboundPerfomanceCounters.AssertCountersExist();
            InboundPerfomanceCounters.AssertCountersExist();
        }

        private void AttachToEvents()
        {
            _queueManager.MessageQueuedForSend += new Action<object, MessageEventArgs>(OnMessageQueuedForSend);
            _queueManager.MessageSent += new Action<object, MessageEventArgs>(OnMessageSent);
            _queueManager.MessageQueuedForReceive += new Action<object, MessageEventArgs>(OnMessageQueuedForReceive);
            _queueManager.MessageReceived += new Action<object, MessageEventArgs>(OnMessageReceived);
        }

        private void DetachFromEvents()
        {
            _queueManager.MessageQueuedForSend -= new Action<object, MessageEventArgs>(OnMessageQueuedForSend);
            _queueManager.MessageSent -= new Action<object, MessageEventArgs>(OnMessageSent);
            _queueManager.MessageQueuedForReceive -= new Action<object, MessageEventArgs>(OnMessageQueuedForReceive);
            _queueManager.MessageReceived -= new Action<object, MessageEventArgs>(OnMessageReceived);
        }

        public void Dispose()
        {
            DetachFromEvents();
        }

        private IInboundPerfomanceCounters GetInboundCounters(MessageEventArgs e) { return GetInboundCounters(_queueManager.InboundInstanceName(e.Message)); }
        private IInboundPerfomanceCounters GetInboundCounters(string instanceName) { return GetPerformanceCounterProviderForCurrentTransaction().GetInboundCounters(instanceName); }
        private IOutboundPerfomanceCounters GetOutboundCounters(MessageEventArgs e) { return GetOutboundCounters(e.Endpoint.OutboundInstanceName(e.Message)); }
        private IOutboundPerfomanceCounters GetOutboundCounters(string instanceName) { return GetPerformanceCounterProviderForCurrentTransaction().GetOutboundCounters(instanceName); }

        private IPerformanceCountersProvider GetPerformanceCounterProviderForCurrentTransaction()
        {
            IPerformanceCountersProvider provider;
            TransactionCompletedEventHandler handler = null;
            var current = Transaction.Current;
            if (current == null)
                return ImmediatelyRecordingProvider;
            if (_transactionalProviders.TryGetValue(current, out provider))
                return provider;
            lock (_transactionalProviders)
                if (!_transactionalProviders.TryGetValue(current, out provider))
                {
                    _transactionalProviders.Add(current, (provider = new TransactionalPerformanceCountersProvider(current, ImmediatelyRecordingProvider)));
                    if (handler == null)
                        handler = delegate(object s, TransactionEventArgs e)
                        {
                            lock (_transactionalProviders)
                                _transactionalProviders.Remove(e.Transaction);
                        };
                    current.TransactionCompleted += handler;
                }
            return provider;
        }

        private void OnMessageQueuedForReceive(object source, MessageEventArgs e)
        {
            var inboundCounters = GetInboundCounters(e);
            lock (inboundCounters)
                inboundCounters.ArrivedMessages++;
        }

        private void OnMessageQueuedForSend(object source, MessageEventArgs e)
        {
            var outboundCounters = GetOutboundCounters(e);
            lock (outboundCounters)
                outboundCounters.UnsentMessages++;
        }

        private void OnMessageReceived(object source, MessageEventArgs e)
        {
            var inboundCounters = GetInboundCounters(e);
            lock (inboundCounters)
                inboundCounters.ArrivedMessages--;
        }

        private void OnMessageSent(object source, MessageEventArgs e)
        {
            var outboundCounters = GetOutboundCounters(e);
            lock (outboundCounters)
                outboundCounters.UnsentMessages--;
        }

        private void SyncWithCurrentQueueState()
        {
            _logger.Debug("Begin synchronization of performance counters with queue state.");
            foreach (string str in _queueManager.Queues)
            {
                var numberOfMessages = _queueManager.GetNumberOfMessages(str);
                foreach (string str2 in _queueManager.GetSubqueues(str))
                {
                    var length = _queueManager.GetAllMessages(str, str2).Length;
                    numberOfMessages -= length;
                    var counters = GetInboundCounters(_queueManager.InboundInstanceName(str, str2));
                    lock (counters)
                        counters.ArrivedMessages = length;
                }
                var inboundCounters = GetInboundCounters(_queueManager.InboundInstanceName(str, string.Empty));
                lock (inboundCounters)
                    inboundCounters.ArrivedMessages = numberOfMessages;
            }
            foreach (var type in from m in _queueManager.GetMessagesCurrentlySending()
                                 group m by m.Endpoint.OutboundInstanceName(m) into c
                                 select new { InstanceName = c.Key, Count = c.Count<PersistentMessageToSend>() })
            {
                var outboundCounters = GetOutboundCounters(type.InstanceName);
                lock (outboundCounters)
                    outboundCounters.UnsentMessages = type.Count;
            }
            _logger.Debug("Synchronization complete.");
        }

        protected virtual IPerformanceCountersProvider ImmediatelyRecordingProvider
        {
            get { return _immediatelyRecordingProvider; }
        }
    }
}

