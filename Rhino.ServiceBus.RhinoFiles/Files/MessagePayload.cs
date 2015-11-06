using System;
using System.Collections.Specialized;

namespace Rhino.Files
{
    public class MessagePayload
    {
        public MessagePayload()
        {
            Headers = new NameValueCollection();
        }

        public byte[] Data { get; set; }
        public DateTime? DeliverBy { get; set; }
        public NameValueCollection Headers { get; set; }
        public int? MaxAttempts { get; set; }
    }
}

