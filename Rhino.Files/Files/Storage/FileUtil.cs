using Rhino.Files.Exceptions;
using Rhino.Files.Model;
using System;
using System.IO;
using System.Threading;

namespace Rhino.Files.Storage
{
    internal static class FileUtil
    {
        static readonly DateTime _fileSystemSafeDate = new DateTime(1900, 1, 1);
        static long _ticksSeed = DateTime.Now.Ticks;
        static volatile int _ticksInc;

        public static MessageId ToMessageId(string path, bool hasTime = true)
        {
            var offset = (hasTime ? 1 : 0);
            var parts = Path.GetFileNameWithoutExtension(path).Split('_');
            return (parts.Length == offset ? new MessageId { SourceInstanceId = Guid.Empty, MessageIdentifier = new Guid(parts[offset]) } : new MessageId { SourceInstanceId = new Guid(parts[offset]), MessageIdentifier = new Guid(parts[offset + 1]) });
        }

        public static string SearchMessageId(MessageId id, string extension, bool hasTime = true)
        {
            if (id == null)
                return (extension == null ? "*" : "*." + extension);
            var pattern = (hasTime ? "????????????????_" : string.Empty) + (id.SourceInstanceId != Guid.Empty ? id.SourceInstanceId + "_" : string.Empty) + id.MessageIdentifier;
            return (extension == null ? pattern + ".*" : pattern + "." + extension);
        }

        public static string SearchTransactionId(Guid id, string extension)
        {
            if (id == Guid.Empty)
                return (extension == null ? "*" : "*." + extension);
            return (extension == null ? id.ToString() + ".*" : id.ToString() + "." + extension);
        }


        public static string FromMessageId(MessageId id, string extension, bool hasTime = true)
        {
            var ticks = _ticksSeed + Interlocked.Increment(ref _ticksInc);
            return (hasTime ? ticks.ToString("X16") + "_" : string.Empty) + (id.SourceInstanceId == Guid.Empty ? string.Empty : id.SourceInstanceId + "_") + id.MessageIdentifier + (extension != null ? "." + extension : string.Empty);
        }

        public static string FromTransactionId(Guid id, string extension)
        {
            return id + (extension != null ? "." + extension : string.Empty);
        }

        public static Guid ToTransactionId(string path)
        {
            return new Guid(Path.GetFileNameWithoutExtension(path));
        }

        public static string NewExtension(string path, string newExtension)
        {
            return path.Substring(0, path.Length - Path.GetExtension(path).Length) + "." + newExtension;
        }

        public static T ParseExtension<T>(string path)
        {
            return (T)Enum.Parse(typeof(T), Path.GetExtension(path).Substring(1));
        }

        public static string MoveBase(string path, string oldBase, string newBase, string newExtension)
        {
            // ERROR IF MOVING BETWEEN SIBLINGS - MoveTo(subqueue)
            var newPath = Path.Combine(newBase, path.Substring(oldBase.Length + 1));
            if (newExtension != null)
                newPath = NewExtension(newPath, newExtension);
            File.Move(path, newPath);
            return newPath;
        }

        public static string MoveBase(string path, string newBase, string newExtension)
        {
            var newPath = Path.Combine(newBase, Path.GetFileNameWithoutExtension(path));
            if (newExtension != null)
                newPath = NewExtension(newPath, newExtension);
            File.Move(path, newPath);
            return newPath;
        }

        public static string MoveExtension(string path, string newExtension)
        {
            var lastWriteTime = File.GetLastWriteTime(path);
            var newPath = NewExtension(path, newExtension);
            if (!File.Exists(path))
                throw new ErrorException { Error = ErrorException.ErrorType.WriteConflict };
            File.Move(path, newPath);
            File.SetLastWriteTime(newPath, lastWriteTime);
            return newPath;
        }

        public static void SetCreationTime(string path, DateTime time)
        {
            File.SetCreationTime(path, (time != DateTime.MinValue ? time : _fileSystemSafeDate));
        }

        internal static void SetLastWriteTime(string path, DateTime time)
        {
            File.SetLastWriteTime(path, (time != DateTime.MinValue ? time : _fileSystemSafeDate));
        }

        internal static void EnsureQueuePath(string path)
        {
            var name = Path.GetFileName(path);
            if (name[0] != '_')
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return;
            }
            if (name.StartsWith("_wait"))
                Thread.Sleep(5000);
            throw new Exception(name);
        }
    }
}

