using System;
using System.Runtime.Serialization;

namespace Rhino.Files.Exceptions
{
    [Serializable]
    public class CannotSendWhileWaitingForAllMessagesToBeSentException : Exception
    {
        public CannotSendWhileWaitingForAllMessagesToBeSentException() { }
        public CannotSendWhileWaitingForAllMessagesToBeSentException(string message) : base(message) { }
        protected CannotSendWhileWaitingForAllMessagesToBeSentException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public CannotSendWhileWaitingForAllMessagesToBeSentException(string message, Exception inner) : base(message, inner) { }
    }
}

