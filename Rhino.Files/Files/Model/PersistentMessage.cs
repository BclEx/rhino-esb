using Rhino.Files.Storage;

namespace Rhino.Files.Model
{
    public class PersistentMessage : Message
    {
        public MessageBookmark Bookmark { get; set; }
        public MessageStatus Status { get; set; }
    }
}