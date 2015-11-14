using System;

namespace Rhino.Files.Storage
{
    public class MessageBookmark : IEquatable<MessageBookmark>
    {
        public bool Equals(MessageBookmark other)
        {
            if (object.ReferenceEquals(null, other))
                return false;
            if (!object.ReferenceEquals(this, other))
            {
                if (!string.Equals(QueueName, other.QueueName))
                    return false;
                if (!string.Equals(Bookmark, other.Bookmark))
                    return false;
            }
            return true;
        }
        public override bool Equals(object obj) { return Equals(obj as MessageBookmark); }
        public override int GetHashCode() { int num = (QueueName.GetHashCode() * 0x18d); return ((num * 0x18d) ^ Bookmark.GetHashCode()); }

        public string Bookmark = string.Empty;
        public string QueueName;
    }
}