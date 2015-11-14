using Rhino.Files.Storage;
using System;
using System.Collections.Specialized;

namespace Rhino.Files.Model
{
    public class PersistentMessageToSend : PersistentMessage
    {
        public string Endpoint { get; set; }
        public OutgoingMessageStatus OutgoingStatus { get; set; }
    }
}