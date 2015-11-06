using Common.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Rhino.Files.Monitoring
{
    public interface IOutboundPerfomanceCounters
    {
        int UnsentMessages { get; set; }
    }

    public class OutboundPerfomanceCounters : IOutboundPerfomanceCounters
    {
        public const string CATEGORY = "Rhino-Files Outbound";
        public const string UNSENT_COUNTER_NAME = "Unsent Messages";
        readonly string _instanceName;
        readonly ILog _logger = LogManager.GetLogger(typeof(OutboundPerfomanceCounters));
        readonly PerformanceCounter _unsentMessages;

        public OutboundPerfomanceCounters(string instanceName)
        {
            _instanceName = instanceName;
            try { _unsentMessages = new PerformanceCounter("Rhino-Files Outbound", "Unsent Messages", instanceName, false); }
            catch (InvalidOperationException e) { throw new ApplicationException(string.Format("Failed to create performance counter: {0}:{1}.  The most likely cause is that the counter categories installed on this machine are out of date.  Use the Rhino.Files.Monitoring.PerformanceCategoryCreator class to re-install the categories.", "Rhino-Files Outbound", "Unsent Messages"), e); }
        }

        public static void AssertCountersExist()
        {
            if (!PerformanceCounterCategory.Exists("Rhino-Files Outbound"))
                throw new ApplicationException(string.Format("The expected performance counter category does not exist: {0}.  The most likely cause is that the needed performance counter categories have not been installed on this machine.  Use the Rhino.Files.Monitoring.PerformanceCategoryCreator class to install the need categories.", "Rhino-Files Outbound"));
        }

        public static IEnumerable<CounterCreationData> SupportedCounters()
        {
            yield return new CounterCreationData
            {
                CounterType = PerformanceCounterType.NumberOfItems32,
                CounterName = "Unsent Messages",
                CounterHelp = "Indicates the number of messages that are awaiting delivery to a queue.  Enable logging on the local queue to get more detailed diagnostic information."
            };
        }

        public int UnsentMessages
        {
            get { return (int)_unsentMessages.RawValue; }
            set
            {
                _logger.DebugFormat("Setting UnsentMessages for instance '{0}' to {1}", _instanceName, value);
                _unsentMessages.RawValue = value;
            }
        }
    }

    internal class TransactionalOutboundPerformanceCounters : IOutboundPerfomanceCounters
    {
        public void CommittTo(IOutboundPerfomanceCounters counters)
        {
            lock (counters)
                counters.UnsentMessages += UnsentMessages;
        }

        public int UnsentMessages { get; set; }
    }
}

