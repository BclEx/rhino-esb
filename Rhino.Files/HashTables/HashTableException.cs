using System;
using System.Runtime.Serialization;

namespace Rhino.HashTables
{
    [Serializable]
    public class HashTableException : Exception
    {
        public HashTableException() { }
        public HashTableException(string message) : base(message) { }
        protected HashTableException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public HashTableException(string message, Exception inner) : base(message, inner) { }

        public HashTableError Error { get; set; }
    }
}