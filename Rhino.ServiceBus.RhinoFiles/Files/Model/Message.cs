using System;
using System.Collections.Specialized;

namespace Rhino.Files.Model
{
    public class Message
    {
        public Message()
        {
            Headers = new NameValueCollection();
        }

        public byte[] Data { get; set; }
        public NameValueCollection Headers { get; set; }
        public MessageId Id { get; set; }
        public string Queue { get; set; }
        public DateTime SentAt { get; set; }
        public string SubQueue { get; set; }
    }
}