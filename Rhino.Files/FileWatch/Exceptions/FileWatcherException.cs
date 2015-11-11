using System;
using System.Runtime.Serialization;

namespace Rhino.FileWatch.Exceptions
{
    public class FileWatcherException : Exception
    {
        public FileWatcherException() { }
        public FileWatcherException(string message) : base(message) { }
        protected FileWatcherException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public FileWatcherException(string message, Exception inner) : base(message, inner) { }
        internal FileWatcherException(FileWatcherError error)
        {
            Error = error;
        }

        public FileWatcherError Error { get; private set; }
    }
}
