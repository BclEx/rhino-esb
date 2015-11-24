using System;
using System.Runtime.Serialization;

namespace Rhino.Files.Exceptions
{
    [Serializable]
    public class ErrorException : Exception
    {
        public enum ErrorType
        {
            WriteConflict,
        }

        public ErrorException() { }
        public ErrorException(string message) : base(message) { }
        protected ErrorException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public ErrorException(string message, Exception inner) : base(message, inner) { }

        public ErrorType Error { get; set; }
    }
}

