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
                if (!string.Equals(QueueName, other.QueueName) || (Size != other.Size))
                    return false;
                for (int i = 0; i < Size; i++)
                    if (Bookmark[i] != other.Bookmark[i])
                        return false;
            }
            return true;
        }
        public override bool Equals(object obj) { return Equals(obj as MessageBookmark); }
        public override int GetHashCode() { int num = (QueueName.GetHashCode() * 0x18d) ^ Size; return ((num * 0x18d) ^ Bookmark.GetHashCode()); }

        public byte[] Bookmark = new byte[1];
        public string QueueName;
        public int Size = 1;
    }
}