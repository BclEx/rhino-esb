using System;

namespace Rhino.Files.Model
{
    public class HistoryMessage : PersistentMessage
    {
        public DateTime MovedToHistoryAt { get; set; }
    }
}