using Common.Logging;
using Newtonsoft.Json;
using Rhino.Files.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Outgoing = Rhino.Files.Storage.GlobalActions.Outgoing;

namespace Rhino.Files.Storage
{
    public class SenderActions : AbstractActions
    {
        readonly QueueManagerConfiguration _configuration;
        readonly ILog _logger;
        readonly string _outgoingPath;
        readonly string _outgoingHistoryPath;

        public SenderActions(string database, Guid instanceId, QueueManagerConfiguration configuration)
            : base(database, instanceId)
        {
            _logger = LogManager.GetLogger(typeof(GlobalActions));
            _configuration = configuration;
            _outgoingPath = Path.Combine(database, ".outgoing");
            _outgoingHistoryPath = Path.Combine(database, ".outgoingHistory");
        }

        public IList<PersistentMessage> GetMessagesToSendAndMarkThemAsInFlight(int maxNumberOfMessage, int maxSizeOfMessagesInTotal, out string endPoint)
        {
            endPoint = null;
            if (!Directory.Exists(_outgoingPath))
                return Enumerable.Empty<PersistentMessage>().ToList();

            string queue = null;
            var messages = new List<PersistentMessage>();
            foreach (var path in Directory.EnumerateFiles(_outgoingPath, FileUtil.SearchMessageId(null, OutgoingMessageStatus.Ready.ToString())).OrderBy(x => x))
            {
                var obj = JsonConvert.DeserializeObject<Outgoing>(File.ReadAllText(path));
                var msgId = obj.msgId;
                var status = FileUtil.ParseExtension<OutgoingMessageStatus>(path);
                var time = obj.timeToSend;

                _logger.DebugFormat("Scanning message {0} with status {1} to be sent at {2}", msgId, status, time);
                if (status != OutgoingMessageStatus.Ready)
                    continue;

                // Check if the message has expired, and move it to the outgoing history.
                var deliverBy = obj.deliverBy;
                if (deliverBy != null && deliverBy.Value < DateTime.Now)
                {
                    _logger.InfoFormat("Outgoing message {0} was not succesfully sent by its delivery time limit {1}", msgId, deliverBy.Value);
                    var numOfRetries = obj.numberOfRetries;
                    MoveFailedMessageToOutgoingHistory(path, numOfRetries, msgId);
                    continue;
                }

                var maxAttempts = obj.maxAttempts;
                if (maxAttempts != null)
                {
                    var numOfRetries = obj.numberOfRetries;
                    if (numOfRetries > maxAttempts)
                    {
                        _logger.InfoFormat("Outgoing message {0} has reached its max attempts of {1}", msgId, maxAttempts);
                        MoveFailedMessageToOutgoingHistory(path, numOfRetries, msgId);
                        continue;
                    }
                }

                if (time > DateTime.Now)
                    continue;

                var rowEndpoint = obj.address;
                if (endPoint == null)
                    endPoint = rowEndpoint;
                if (!endPoint.Equals(rowEndpoint))
                    continue;

                var rowQueue = obj.queue;
                if (queue == null)
                    queue = rowQueue;
                if (queue != rowQueue)
                    continue;

                var bookmark = new MessageBookmark { Bookmark = FileUtil.MoveExtension(path, OutgoingMessageStatus.InFlight.ToString()) };
                _logger.DebugFormat("Adding message {0} to returned messages", msgId);
                messages.Add(new PersistentMessage
                {
                    Id = new MessageId
                    {
                        SourceInstanceId = _instanceId,
                        MessageIdentifier = msgId
                    },
                    Headers = HttpUtility.ParseQueryString(obj.headers),
                    Queue = rowQueue,
                    SubQueue = obj.subqueue,
                    SentAt = obj.sentAt,
                    Data = obj.data,
                    Bookmark = bookmark
                });
                _logger.DebugFormat("Marking output message {0} as InFlight", msgId);
                if (maxNumberOfMessage < messages.Count)
                    break;
                if (maxSizeOfMessagesInTotal < messages.Sum(x => x.Data.Length))
                    break;
            }
            return messages;
        }

        public void MarkOutgoingMessageAsFailedTransmission(MessageBookmark bookmark, bool queueDoesNotExistsInDestination)
        {
            var path = bookmark.Bookmark;
            if (!path.StartsWith(_outgoingPath) && !File.Exists(path))
                return;
            var obj = JsonConvert.DeserializeObject<Outgoing>(File.ReadAllText(path));
            var numOfRetries = obj.numberOfRetries;
            var msgId = obj.msgId;
            if (numOfRetries < 100 && !queueDoesNotExistsInDestination)
            {
                var timeToSend = DateTime.Now.AddSeconds(numOfRetries * numOfRetries);
                obj.timeToSend = timeToSend;
                obj.numberOfRetries++;
                File.WriteAllText(path, JsonConvert.SerializeObject(obj));
                bookmark.Bookmark = FileUtil.MoveExtension(path, OutgoingMessageStatus.Ready.ToString());
                _logger.DebugFormat("Marking outgoing message {0} as failed with retries: {1}", msgId, numOfRetries);
            }
            else
                MoveFailedMessageToOutgoingHistory(path, numOfRetries, msgId);
        }

        public MessageBookmark MarkOutgoingMessageAsSuccessfullySent(MessageBookmark bookmark)
        {
            var path = bookmark.Bookmark;
            if (!path.StartsWith(_outgoingPath))
                return null;
            if (!Directory.Exists(_outgoingHistoryPath))
                Directory.CreateDirectory(_outgoingHistoryPath);
            //var obj = JsonConvert.DeserializeObject<Outgoing>(File.ReadAllText(path));
            var msgId = "msgId"; //obj.msgId;
            var newBookmark = new MessageBookmark { Bookmark = FileUtil.MoveBase(path, _outgoingPath, _outgoingHistoryPath, OutgoingMessageStatus.Sent.ToString()) };
            _logger.DebugFormat("Successfully sent output message {0}", msgId);
            return newBookmark;
        }

        public bool HasMessagesToSend()
        {
            if (!Directory.Exists(_outgoingPath))
                return false;
            return Directory.EnumerateFiles(_outgoingPath).Any();
        }

        public IEnumerable<PersistentMessageToSend> GetMessagesToSend()
        {
            if (!Directory.Exists(_outgoingPath))
                return Enumerable.Empty<PersistentMessageToSend>();
            return Directory.EnumerateFiles(_outgoingPath)
                .Select(x =>
                {
                    var obj = JsonConvert.DeserializeObject<Outgoing>(File.ReadAllText(x));
                    return new PersistentMessageToSend
                    {
                        Id = new MessageId
                        {
                            SourceInstanceId = _instanceId,
                            MessageIdentifier = obj.msgId,
                        },
                        OutgoingStatus = FileUtil.ParseExtension<OutgoingMessageStatus>(x),
                        Endpoint = obj.address,
                        Queue = obj.queue,
                        SubQueue = obj.subqueue,
                        SentAt = obj.sentAt,
                        Data = obj.data,
                        Bookmark = new MessageBookmark { Bookmark = x },
                    };
                });
        }

        public void RevertBackToSend(MessageBookmark[] bookmarks)
        {
            foreach (var bookmark in bookmarks)
            {
                var path = bookmark.Bookmark;
                if (!path.StartsWith(_outgoingHistoryPath))
                    return;

                var obj = JsonConvert.DeserializeObject<Outgoing>(File.ReadAllText(path));
                var msgId = obj.msgId;
                obj.numberOfRetries++;
                _logger.DebugFormat("Reverting output message {0} back to Ready mode", msgId);
                File.WriteAllText(path, JsonConvert.SerializeObject(obj));
                bookmark.Bookmark = FileUtil.MoveBase(path, _outgoingHistoryPath, _outgoingPath, OutgoingMessageStatus.Ready.ToString());
            }
        }

        private void MoveFailedMessageToOutgoingHistory(string path, int numOfRetries, Guid msgId)
        {
            if (_configuration.EnableOutgoingMessageHistory)
            {
                if (!Directory.Exists(_outgoingHistoryPath))
                    Directory.CreateDirectory(_outgoingHistoryPath);
                FileUtil.MoveBase(path, _outgoingHistoryPath, OutgoingMessageStatus.Failed.ToString());
                _logger.DebugFormat("Marking outgoing message {0} as permenantly failed after {1} retries", msgId, numOfRetries);
            }
            else
                File.Delete(path);
        }
    }
}

