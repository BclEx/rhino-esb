using Common.Logging;
using System;
using System.Diagnostics;
using System.Linq;

namespace Rhino.Files.Monitoring
{
    public static class PerformanceCategoryCreator
    {
        internal const string CANT_CREATE_COUNTER_MSG = "Failed to create performance counter: {0}:{1}.  The most likely cause is that the counter categories installed on this machine are out of date.  Use the Rhino.Files.Monitoring.PerformanceCategoryCreator class to re-install the categories.";
        internal const string CATEGORY_DOES_NOT_EXIST = "The expected performance counter category does not exist: {0}.  The most likely cause is that the needed performance counter categories have not been installed on this machine.  Use the Rhino.Files.Monitoring.PerformanceCategoryCreator class to install the need categories.";
        static readonly ILog _logger = LogManager.GetLogger(typeof(PerformanceCategoryCreator));

        public static void CreateCategories()
        {
            try { CreateOutboundCategory(); CreateInboundCategory(); }
            catch (UnauthorizedAccessException e) { _logger.Error("Not authorized to create performance counters. User must be an administrator to perform this action.", e); throw; }
        }

        private static void CreateInboundCategory()
        {
            if (PerformanceCounterCategory.Exists("Rhino-Files Inbound"))
            {
                _logger.DebugFormat("Deleting existing performance counter category '{0}'.", "Rhino-Files Inbound");
                PerformanceCounterCategory.Delete("Rhino-Files Inbound");
            }
            _logger.DebugFormat("Creating performance counter category '{0}'.", "Rhino-Files Inbound");
            try
            {
                var counterData = new CounterCreationDataCollection(InboundPerfomanceCounters.SupportedCounters().ToArray());
                PerformanceCounterCategory.Create("Rhino-Files Inbound", "Provides statistics for Rhino-Files messages in-bound to queues on the current machine.", PerformanceCounterCategoryType.MultiInstance, counterData);
            }
            catch (Exception e) { _logger.Error("Creation of inbound counters failed.", e); throw; }
        }

        private static void CreateOutboundCategory()
        {
            if (PerformanceCounterCategory.Exists("Rhino-Files Outbound"))
            {
                _logger.DebugFormat("Deleting existing performance counter category '{0}'.", "Rhino-Files Outbound");
                PerformanceCounterCategory.Delete("Rhino-Files Outbound");
            }
            _logger.DebugFormat("Creating performance counter category '{0}'.", "Rhino-Files Outbound");
            try
            {
                var counterData = new CounterCreationDataCollection(OutboundPerfomanceCounters.SupportedCounters().ToArray());
                PerformanceCounterCategory.Create("Rhino-Files Outbound", "Provides statistics for Rhino-Files messages out-bound from the current machine.", PerformanceCounterCategoryType.MultiInstance, counterData);
            }
            catch (Exception e) { _logger.Error("Creation of outbound counters failed.", e); throw; }
        }
    }
}

