using Rhino.Files.Utils;
using System;

namespace Rhino.Files.Model
{
    public class MessageId
    {
        public bool Equals(MessageId other)
        {
            if (object.ReferenceEquals(null, other)) return false;
            return (object.ReferenceEquals(this, other) || (other.SourceInstanceId.Equals(SourceInstanceId) && other.MessageIdentifier.Equals(MessageIdentifier)));
        }
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(null, obj)) return false;
            if (object.ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(MessageId)) return false;
            return Equals((MessageId)obj);
        }
        public override int GetHashCode() { return ((SourceInstanceId.GetHashCode() * 0x18d) ^ MessageIdentifier.GetHashCode()); }
        public override string ToString() { return string.Format("{0}/{1}", SourceInstanceId, MessageIdentifier); }

        public Guid MessageIdentifier { get; set; }
        public Guid SourceInstanceId { get; set; }

        public static MessageId GenerateRandom()
        {
            return new MessageId { SourceInstanceId = Guid.NewGuid(), MessageIdentifier = GuidCombGenerator.Generate() };
        }
    }
}