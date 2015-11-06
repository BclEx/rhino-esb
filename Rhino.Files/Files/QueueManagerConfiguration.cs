using System;

namespace Rhino.Files
{
    public class QueueManagerConfiguration
    {
        public QueueManagerConfiguration()
        {
            EnableOutgoingMessageHistory = true;
            EnableProcessedMessageHistory = true;
            NumberOfMessagesToKeepInOutgoingHistory = 100;
            NumberOfMessagesToKeepInProcessedHistory = 100;
            NumberOfReceivedMessageIdsToKeep = 0x2710;
            OldestMessageInOutgoingHistory = TimeSpan.FromDays(1.0);
            OldestMessageInProcessedHistory = TimeSpan.FromDays(1.0);
        }

        public bool EnableOutgoingMessageHistory { get; set; }
        public bool EnableProcessedMessageHistory { get; set; }
        public int NumberOfMessagesToKeepInOutgoingHistory { get; set; }
        public int NumberOfMessagesToKeepInProcessedHistory { get; set; }
        public int NumberOfReceivedMessageIdsToKeep { get; set; }
        public TimeSpan OldestMessageInOutgoingHistory { get; set; }
        public TimeSpan OldestMessageInProcessedHistory { get; set; }
    }
}