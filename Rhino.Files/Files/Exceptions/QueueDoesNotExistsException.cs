using System;
using System.Runtime.Serialization;

namespace Rhino.Files.Exceptions
{
    [Serializable]
    public class QueueDoesNotExistsException : Exception
    {
        public QueueDoesNotExistsException() { }
        public QueueDoesNotExistsException(string message) : base(message) { }
        protected QueueDoesNotExistsException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public QueueDoesNotExistsException(string message, Exception inner) : base(message, inner) { }
    }
}

