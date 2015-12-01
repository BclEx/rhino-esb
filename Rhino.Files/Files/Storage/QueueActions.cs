using Common.Logging;
using Newtonsoft.Json;
using Rhino.Files.Exceptions;
using Rhino.Files.Model;
using Rhino.Files.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using QueueMsg = Rhino.Files.Storage.GlobalActions.QueueMsg;

namespace Rhino.Files.Storage
{
    public class QueueActions : AbstractActions
    {
        readonly ILog _logger = LogManager.GetLogger(typeof(GlobalActions));
        readonly string _queueName;
        string[] _subqueues;
        readonly AbstractActions _actions;
        readonly Action<int> _changeNumberOfMessages;
        readonly string _msgsPath;
        readonly string _msgsHistoryPath;

        public QueueActions(string database, Guid instanceId, string queueName, string[] subqueues, AbstractActions actions, Action<int> changeNumberOfMessages)
            : base(database, instanceId)
        {
            _queueName = queueName;
            _subqueues = subqueues;
            _actions = actions;
            _changeNumberOfMessages = changeNumberOfMessages;
            _msgsPath = Path.Combine(database, queueName);
            _msgsHistoryPath = Path.Combine(database, queueName + "_history");
        }

        #region Queue

        public string[] Subqueues
        {
            get { return _subqueues; }
        }

        public MessageBookmark Enqueue(Message message)
        {
            if (!Directory.Exists(_msgsPath))
                throw new QueueDoesNotExistsException(_queueName);
            if (!string.IsNullOrEmpty(message.SubQueue) && !Subqueues.Contains(message.SubQueue))
            {
                _actions.AddSubqueueTo(_queueName, message.SubQueue);
                _subqueues = _subqueues.Union(new[] { message.SubQueue }).ToArray();
            }
            var path = (string.IsNullOrEmpty(message.SubQueue) ? _msgsPath : Path.Combine(_msgsPath, message.SubQueue));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var messageStatus = MessageStatus.InTransit;
            var persistentMessage = (message as PersistentMessage);
            if (persistentMessage != null)
                messageStatus = persistentMessage.Status;

            var obj = new QueueMsg
            {
                data = message.Data,
                headers = message.Headers.ToQueryString(),
            };
            var path2 = Path.Combine(path, FileUtil.FromMessageId(message.Id, messageStatus.ToString()));
            File.WriteAllText(path2, JsonConvert.SerializeObject(obj));
            FileUtil.SetCreationTime(path2, message.SentAt);
            var bm = new MessageBookmark { Bookmark = path2, QueueName = _queueName };

            _logger.DebugFormat("Enqueuing msg to '{0}' with subqueue: '{1}'. Id: {2}", _queueName, message.SubQueue, message.Id);
            _changeNumberOfMessages(1);
            return bm;
        }

        public PersistentMessage Dequeue(string subQueue)
        {
            var path = (string.IsNullOrEmpty(subQueue) ? _msgsPath : Path.Combine(_msgsPath, subQueue));
            if (!Directory.Exists(path))
                throw new QueueDoesNotExistsException(_queueName);
            return Directory.EnumerateFiles(path, FileUtil.SearchMessageId(null, MessageStatus.ReadyToDeliver.ToString()))
                .OrderBy(x => x)
                .Select(x =>
                {
                    var id = FileUtil.ToMessageId(x);
                    var status = FileUtil.ParseExtension<MessageStatus>(x);
                    _logger.DebugFormat("Scanning incoming message {2} on '{0}/{1}' with status {3}", _queueName, subQueue, id, status);
                    if (status != MessageStatus.ReadyToDeliver)
                        return null;

                    var obj = JsonConvert.DeserializeObject<QueueMsg>(File.ReadAllText(x));
                    try { x = FileUtil.MoveExtension(x, MessageStatus.Processing.ToString()); }
                    catch (ErrorException e)
                    {
                        _logger.DebugFormat("Write conflict on '{0}/{1}' for {2}, skipping message", _queueName, subQueue, id);
                        if (e.Error == ErrorException.ErrorType.WriteConflict)
                            return null;
                        throw;
                    }
                    var bookmark = new MessageBookmark { Bookmark = x, QueueName = _queueName };
                    _changeNumberOfMessages(-1);

                    _logger.DebugFormat("Dequeuing message {2} from '{0}/{1}'", _queueName, subQueue, id);
                    return new PersistentMessage
                    {
                        Bookmark = bookmark,
                        Headers = HttpUtility.ParseQueryString(obj.headers),
                        Queue = _queueName,
                        SentAt = File.GetCreationTime(x),
                        Data = obj.data,
                        Id = id,
                        SubQueue = subQueue,
                        Status = status,
                    };
                })
                .Where(x => x != null)
                .FirstOrDefault();
        }

        public void SetMessageStatus(MessageBookmark bookmark, MessageStatus status, string subQueue = null)
        {
            var path = bookmark.Bookmark;
            var id = FileUtil.ToMessageId(path);
            if (!File.Exists(path))
                return;
            bookmark.Bookmark = FileUtil.MoveExtension(path, status.ToString());
            if (subQueue == null)
                _logger.DebugFormat("Changing message {0} status to {1} on {2}", id, status, _queueName);
            else
                _logger.DebugFormat("Changing message {0} status to {1} on queue '{2}' and set subqueue to '{3}'", id, status, _queueName, subQueue);
        }

        #endregion

        public void MoveToHistory(MessageBookmark bookmark)
        {
            var path = bookmark.Bookmark;
            var id = FileUtil.ToMessageId(path);
            if (!File.Exists(path))
                return;
            bookmark.Bookmark = FileUtil.MoveBase(path, _msgsPath, _msgsHistoryPath, null);
            FileUtil.SetLastWriteTime(bookmark.Bookmark, DateTime.Now);
            _logger.DebugFormat("Moving message {0} on queue {1} to history", id, _queueName);
        }

        public IEnumerable<PersistentMessage> GetAllMessages(string subQueue)
        {
            var path = (string.IsNullOrEmpty(subQueue) ? _msgsPath : Path.Combine(_msgsPath, subQueue));
            if (!Directory.Exists(path))
                return null;
            return Directory.EnumerateFiles(path)
                .OrderBy(x => x)
                .Select(x =>
                {
                    var status = FileUtil.ParseExtension<MessageStatus>(x);
                    var obj = JsonConvert.DeserializeObject<QueueMsg>(File.ReadAllText(x));
                    var bookmark = new MessageBookmark { Bookmark = x, QueueName = _queueName };
                    return new PersistentMessage
                    {
                        Bookmark = bookmark,
                        Headers = HttpUtility.ParseQueryString(obj.headers),
                        Queue = _queueName,
                        Status = status,
                        SentAt = File.GetCreationTime(x),
                        Data = obj.data,
                        SubQueue = subQueue,
                        Id = FileUtil.ToMessageId(x),
                    };
                });
        }

        public IEnumerable<HistoryMessage> GetAllProcessedMessages(int? batchSize = null)
        {
            var path = _msgsHistoryPath;
            if (!Directory.Exists(path))
                return Enumerable.Empty<HistoryMessage>();
            var query = Directory.EnumerateFiles(path)
                .OrderBy(x => x)
                .Select(x =>
                {
                    var status = FileUtil.ParseExtension<MessageStatus>(x);
                    var obj = JsonConvert.DeserializeObject<QueueMsg>(File.ReadAllText(x));
                    var bookmark = new MessageBookmark { Bookmark = x, QueueName = _queueName };
                    return new HistoryMessage
                    {
                        Bookmark = bookmark,
                        Headers = HttpUtility.ParseQueryString(obj.headers),
                        Queue = _queueName,
                        MovedToHistoryAt = File.GetLastWriteTime(x),
                        Status = status,
                        SentAt = File.GetCreationTime(x),
                        Data = obj.data,
                        Id = FileUtil.ToMessageId(x),
                    };
                });
            return (batchSize == null ? query : query.Take(batchSize.Value));
        }

        public MessageBookmark MoveTo(string subQueue, PersistentMessage message)
        {
            var path = message.Bookmark.Bookmark;
            var id = FileUtil.ToMessageId(path);
            if (!File.Exists(path))
                return null;
            var msgPath = Path.Combine(_msgsPath, subQueue);
            var newPath = FileUtil.MoveBase(path, _msgsPath, msgPath, MessageStatus.SubqueueChanged.ToString());
            var bookmark = new MessageBookmark { Bookmark = newPath, QueueName = _queueName };
            _logger.DebugFormat("Moving message {0} to subqueue {1}", id, _queueName);
            return bookmark;
        }

        public MessageStatus GetMessageStatus(MessageBookmark bookmark)
        {
            var path = bookmark.Bookmark;
            return FileUtil.ParseExtension<MessageStatus>(path);
        }

        public void Delete(MessageBookmark bookmark)
        {
            var path = bookmark.Bookmark;
            if (File.Exists(path) && path.StartsWith(_msgsPath))
                File.Delete(path);
        }

        public PersistentMessage Peek(string subQueue)
        {
            var path = (string.IsNullOrEmpty(subQueue) ? _msgsPath : Path.Combine(_msgsPath, subQueue));
            if (!Directory.Exists(path))
                return null;
            return Directory.EnumerateFiles(path, FileUtil.SearchMessageId(null, MessageStatus.ReadyToDeliver.ToString()))
                .OrderBy(x => x)
                .Select(x =>
                {
                    var id = FileUtil.ToMessageId(x);
                    var status = FileUtil.ParseExtension<MessageStatus>(x);
                    _logger.DebugFormat("Scanning incoming message {2} on '{0}/{1}' with status {3}", _queueName, subQueue, id, status);
                    if (status != MessageStatus.ReadyToDeliver)
                        return null;

                    var obj = JsonConvert.DeserializeObject<QueueMsg>(File.ReadAllText(x));
                    var bookmark = new MessageBookmark { Bookmark = x, QueueName = _queueName };
                    _logger.DebugFormat("Peeking message {2} from '{0}/{1}'", _queueName, subQueue, id);
                    return new PersistentMessage
                    {
                        Bookmark = bookmark,
                        Headers = HttpUtility.ParseQueryString(obj.headers),
                        Queue = _queueName,
                        SentAt = File.GetCreationTime(x),
                        Data = obj.data,
                        Id = id,
                        SubQueue = subQueue,
                        Status = status,
                    };
                })
                .FirstOrDefault();
        }

        public PersistentMessage PeekById(MessageId id)
        {
            var path = _msgsPath;
            if (!Directory.Exists(path))
                return null;
            var msgsPathLength = _msgsPath.Length;
            return Directory.EnumerateFiles(path, FileUtil.SearchMessageId(id, MessageStatus.ReadyToDeliver.ToString()), SearchOption.AllDirectories)
                .OrderBy(x => x)
                .Select(x =>
                {
                    var subQueue = Path.GetDirectoryName(x).Substring(msgsPathLength);
                    var status = FileUtil.ParseExtension<MessageStatus>(x);
                    if (status != MessageStatus.ReadyToDeliver)
                        return null;

                    var obj = JsonConvert.DeserializeObject<QueueMsg>(File.ReadAllText(x));
                    var bookmark = new MessageBookmark { Bookmark = x, QueueName = _queueName };
                    return new PersistentMessage
                    {
                        Bookmark = bookmark,
                        Headers = HttpUtility.ParseQueryString(obj.headers),
                        Queue = _queueName,
                        SentAt = File.GetCreationTime(x),
                        Data = obj.data,
                        Id = id,
                        SubQueue = (string.IsNullOrEmpty(subQueue) ? null : subQueue),
                        Status = status,
                    };
                })
                .FirstOrDefault();
        }

        public void DeleteHistoric(MessageBookmark bookmark)
        {
            var path = bookmark.Bookmark;
            if (File.Exists(path) && path.StartsWith(_msgsHistoryPath))
                File.Delete(path);
        }

        public MessageBookmark GetMessageHistoryBookmarkAtPosition(int positionFromNewestProcessedMessage)
        {
            var path = _msgsHistoryPath;
            if (!Directory.Exists(path))
                return null;
            return Directory.EnumerateFiles(path)
                .OrderByDescending(x => x)
                .Skip(positionFromNewestProcessedMessage)
                .Select(x => new MessageBookmark { Bookmark = x })
                .FirstOrDefault();
        }
    }
}

