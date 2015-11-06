using System.Collections.Generic;
using System.Transactions;

namespace Rhino.Files.Monitoring
{
    public interface IPerformanceCountersProvider
    {
        IInboundPerfomanceCounters GetInboundCounters(string instanceName);
        IOutboundPerfomanceCounters GetOutboundCounters(string instanceName);
    }

    internal class ImmediatelyRecordingCountersProvider : IPerformanceCountersProvider
    {
        readonly Dictionary<string, IInboundPerfomanceCounters> _inboundCounters = new Dictionary<string, IInboundPerfomanceCounters>();
        readonly Dictionary<string, IOutboundPerfomanceCounters> _outboundCounters = new Dictionary<string, IOutboundPerfomanceCounters>();

        public IInboundPerfomanceCounters GetInboundCounters(string instanceName)
        {
            IInboundPerfomanceCounters counters;
            if (!_inboundCounters.TryGetValue(instanceName, out counters))
                lock (_outboundCounters)
                    if (!_inboundCounters.TryGetValue(instanceName, out counters))
                        _inboundCounters.Add(instanceName, (counters = new InboundPerfomanceCounters(instanceName)));
            return counters;
        }

        public IOutboundPerfomanceCounters GetOutboundCounters(string instanceName)
        {
            IOutboundPerfomanceCounters counters;
            if (!_outboundCounters.TryGetValue(instanceName, out counters))
                lock (_outboundCounters)
                    if (!_outboundCounters.TryGetValue(instanceName, out counters))
                        _outboundCounters.Add(instanceName, (counters = new OutboundPerfomanceCounters(instanceName)));
            return counters;
        }
    }

    public class TransactionalPerformanceCountersProvider : IPerformanceCountersProvider
    {
        readonly Dictionary<string, TransactionalInboundPerformanceCounters> _inboundCounters = new Dictionary<string, TransactionalInboundPerformanceCounters>();
        readonly Dictionary<string, TransactionalOutboundPerformanceCounters> _outboundCounters = new Dictionary<string, TransactionalOutboundPerformanceCounters>();
        readonly IPerformanceCountersProvider _providerToCommitTo;

        public TransactionalPerformanceCountersProvider(Transaction transaction, IPerformanceCountersProvider providerToCommitTo)
        {
            _providerToCommitTo = providerToCommitTo;
            transaction.TransactionCompleted += new TransactionCompletedEventHandler(HandleTransactionCompleted);
        }

        public IInboundPerfomanceCounters GetInboundCounters(string instanceName)
        {
            TransactionalInboundPerformanceCounters counters;
            if (_inboundCounters.TryGetValue(instanceName, out counters))
                return counters;
            lock (_inboundCounters)
                if (!_inboundCounters.TryGetValue(instanceName, out counters))
                    _inboundCounters.Add(instanceName, (counters = new TransactionalInboundPerformanceCounters()));
            return counters;
        }

        public IOutboundPerfomanceCounters GetOutboundCounters(string instanceName)
        {
            TransactionalOutboundPerformanceCounters counters;
            if (_outboundCounters.TryGetValue(instanceName, out counters))
                return counters;
            lock (_outboundCounters)
                if (!_outboundCounters.TryGetValue(instanceName, out counters))
                    _outboundCounters.Add(instanceName, (counters = new TransactionalOutboundPerformanceCounters()));
            return counters;
        }

        private void HandleTransactionCompleted(object sender, TransactionEventArgs e)
        {
            if (e.Transaction.TransactionInformation.Status == TransactionStatus.Committed)
            {
                foreach (KeyValuePair<string, TransactionalOutboundPerformanceCounters> pair in _outboundCounters)
                    pair.Value.CommittTo(_providerToCommitTo.GetOutboundCounters(pair.Key));
                foreach (KeyValuePair<string, TransactionalInboundPerformanceCounters> pair2 in _inboundCounters)
                    pair2.Value.CommittTo(_providerToCommitTo.GetInboundCounters(pair2.Key));
            }
        }
    }
}

