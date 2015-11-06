using Common.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Rhino.Files.Monitoring
{
    public interface IInboundPerfomanceCounters
    {
        int ArrivedMessages { get; set; }
    }

    public class InboundPerfomanceCounters : IInboundPerfomanceCounters
    {
        public const string ARRIVED_COUNTER_NAME = "Arrived Messages";
        private readonly PerformanceCounter _arrivedMessages;
        public const string CATEGORY = "Rhino-Files Inbound";
        private readonly string _instanceName;
        private readonly ILog _logger = LogManager.GetLogger(typeof(InboundPerfomanceCounters));

        public InboundPerfomanceCounters(string instanceName)
        {
            _instanceName = instanceName;
            try { _arrivedMessages = new PerformanceCounter("Rhino-Files Inbound", "Arrived Messages", instanceName, false); }
            catch (InvalidOperationException e) { throw new ApplicationException(string.Format("Failed to create performance counter: {0}:{1}.  The most likely cause is that the counter categories installed on this machine are out of date.  Use the Rhino.Queues.Monitoring.PerformanceCategoryCreator class to re-install the categories.", "Rhino-Files Inbound", "Arrived Messages"), e); }
        }

        public static void AssertCountersExist()
        {
            if (!PerformanceCounterCategory.Exists("Rhino-Files Inbound"))
                throw new ApplicationException(string.Format("The expected performance counter category does not exist: {0}.  The most likely cause is that the needed performance counter categories have not been installed on this machine.  Use the Rhino.Queues.Monitoring.PerformanceCategoryCreator class to install the need categories.", "Rhino-Files Inbound"));
        }

        public static IEnumerable<CounterCreationData> SupportedCounters()
        {
            yield return new CounterCreationData
            {
                CounterType = PerformanceCounterType.NumberOfItems32,
                CounterName = "Arrived Messages",
                CounterHelp = "Indicates the number of messages that have arrived in the queue but have not been received by the queue's client.  Enable logging on the queue to get more detailed diagnostic information."
            };
        }

        public int ArrivedMessages
        {
            get { return (int)_arrivedMessages.RawValue; }
            set
            {
                _logger.DebugFormat("Setting UnsentMessages for instance '{0}' to {1}", _instanceName, value);
                _arrivedMessages.RawValue = value;
            }
        }
    }

    internal class TransactionalInboundPerformanceCounters : IInboundPerfomanceCounters
    {
        public void CommittTo(IInboundPerfomanceCounters counters)
        {
            lock (counters)
                counters.ArrivedMessages += ArrivedMessages;
        }

        public int ArrivedMessages { get; set; }
    }
}

