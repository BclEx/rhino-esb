using Rhino.Files.Model;
using System;

namespace Rhino.Files
{
    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(string endpoint, Message message)
        {
            Endpoint = endpoint;
            Message = message;
        }

        public string Endpoint { get; private set; }
        public Message Message { get; private set; }
    }
}

