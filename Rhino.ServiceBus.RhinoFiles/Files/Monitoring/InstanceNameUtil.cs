using Rhino.Files.Model;

namespace Rhino.Files.Monitoring
{
    internal static class InstanceNameUtil
    {
        public static string InboundInstanceName(this IQueueManager queueManager, Message message) { return queueManager.InboundInstanceName(message.Queue, message.SubQueue); }
        public static string InboundInstanceName(this IQueueManager queueManager, string queue, string subQueue) { return string.Format("{0}/{1}/{2}", queueManager.Endpoint, queue, subQueue).TrimEnd(new char[] { '/' }); }
        public static string OutboundInstanceName(this string endpoint, Message message) { return string.Format("{0}/{1}/{2}", endpoint, message.Queue, message.SubQueue).TrimEnd(new char[] { '/' }); }
    }
}

