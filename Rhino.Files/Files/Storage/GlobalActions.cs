using Common.Logging;
using Newtonsoft.Json;
using Rhino.Files.Model;
using Rhino.Files.Protocol;
using Rhino.Files.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Rhino.Files.Storage
{
    public class GlobalActions : AbstractActions
    {
        readonly QueueManagerConfiguration _configuration;
        readonly ILog _logger;
        readonly string _txsPath;
        readonly string _queuesPath;
        readonly string _recoveryPath;
        readonly string _outgoingPath;
        readonly string _receivedPath;
        readonly string _outgoingHistoryPath;
        MessageBookmark _outgoingBookmark;

        public GlobalActions(string database, Guid instanceId, QueueManagerConfiguration configuration)
            : base(database, instanceId)
        {
            _logger = LogManager.GetLogger(typeof(GlobalActions));
            _configuration = configuration;
            _txsPath = Path.Combine(database, ".txs");
            _queuesPath = Path.Combine(database, ".queues");
            _recoveryPath = Path.Combine(database, ".recovery");
            _outgoingPath = Path.Combine(database, ".outgoing");
            _receivedPath = Path.Combine(database, ".received");
            _outgoingHistoryPath = Path.Combine(database, ".outgoingHistory");
        }

        #region Queue

        public class QueueMsg
        {
            public byte[] data { get; set; }
            public string headers { get; set; }
        }

        public void CreateQueueIfDoesNotExists(string queueName)
        {
            if (!Directory.Exists(_database))
                Directory.CreateDirectory(_database);
            var path = Path.Combine(_database, queueName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        #endregion

        #region Recovery

        public void RegisterRecoveryInformation(Guid transactionId, byte[] information)
        {
            if (!Directory.Exists(_recoveryPath))
                Directory.CreateDirectory(_recoveryPath);
            var path = Path.Combine(_recoveryPath, transactionId.ToString());
            File.WriteAllBytes(path, information);
        }

        public void DeleteRecoveryInformation(Guid transactionId)
        {
            if (!Directory.Exists(_recoveryPath))
                return;
            var path = Path.Combine(_recoveryPath, transactionId.ToString());
            if (File.Exists(path))
                File.Delete(path);
        }

        public IEnumerable<byte[]> GetRecoveryInformation()
        {
            if (!Directory.Exists(_recoveryPath))
                return Enumerable.Empty<byte[]>();
            return Directory.EnumerateFiles(_recoveryPath)
                .Select(File.ReadAllBytes);
        }

        #endregion

        #region Txs

        public class Tx
        {
            public Guid txId { get; set; }
            public string bookmark { get; set; }
            public int valueToRestore { get; set; }
            public string queue { get; set; }
            public string subQueue { get; set; }
        }

        public void RegisterUpdateToReverse(Guid txId, MessageBookmark bookmark, MessageStatus statusToRestore, string subQueue)
        {
            if (!Directory.Exists(_txsPath))
                Directory.CreateDirectory(_txsPath);
            var path = Path.Combine(_txsPath, FileUtil.FromTransactionId(txId, null));
            if (File.Exists(path))
                File.Delete(path);
            var obj = new Tx
            {
                txId = txId,
                bookmark = bookmark.Bookmark,
                valueToRestore = (int)statusToRestore,
                queue = bookmark.QueueName,
                subQueue = subQueue,
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(obj));
        }

        public void RemoveReversalsMoveCompletedMessagesAndFinishSubQueueMove(Guid transactionId)
        {
            if (!Directory.Exists(_txsPath))
                return;
            foreach (var path in Directory.EnumerateFiles(_txsPath, FileUtil.SearchTransactionId(transactionId, null)))
            {
                var obj = JsonConvert.DeserializeObject<Tx>(File.ReadAllText(path));
                var actions = GetQueue(obj.queue);
                var bookmark = new MessageBookmark { Bookmark = obj.bookmark, QueueName = obj.queue };
                switch (actions.GetMessageStatus(bookmark))
                {
                    case MessageStatus.SubqueueChanged:
                    case MessageStatus.EnqueueWait:
                        actions.SetMessageStatus(bookmark, MessageStatus.ReadyToDeliver);
                        break;
                    default:
                        if (_configuration.EnableProcessedMessageHistory)
                            actions.MoveToHistory(bookmark);
                        else
                            actions.Delete(bookmark);
                        break;
                }
                File.Delete(path);
            }
        }

        #endregion

        #region Send

        public class Outgoing
        {
            public Guid msgId { get; set; }
            public Guid txId { get; set; }
            public string address { get; set; }
            public DateTime timeToSend { get; set; }
            public DateTime sentAt { get; set; }
            public string queue { get; set; }
            public string subqueue { get; set; }
            public string headers { get; set; }
            public byte[] data { get; set; }
            public int numberOfRetries { get; set; }
            public int sizeOfData { get; set; }
            public DateTime? deliverBy { get; set; }
            public int? maxAttempts { get; set; }
        }

        public Guid RegisterToSend(string destination, string queue, string subQueue, MessagePayload payload, Guid transactionId)
        {
            if (!Directory.Exists(_outgoingPath))
                Directory.CreateDirectory(_outgoingPath);
            var msgId = GuidCombGenerator.Generate();
            var obj = new Outgoing
            {
                msgId = msgId,
                txId = transactionId,
                address = destination,
                timeToSend = DateTime.Now,
                sentAt = DateTime.Now,
                queue = queue,
                subqueue = subQueue,
                headers = payload.Headers.ToQueryString(),
                data = payload.Data,
                numberOfRetries = 1,
                sizeOfData = payload.Data.Length,
                deliverBy = payload.DeliverBy,
                maxAttempts = payload.MaxAttempts,
            };
            var path = Path.Combine(_outgoingPath, FileUtil.FromTransactionId(transactionId, OutgoingMessageStatus.NotReady.ToString()));
            File.WriteAllText(path, JsonConvert.SerializeObject(obj));
            var bookmark = new MessageBookmark { Bookmark = path };
            _outgoingBookmark = bookmark;
            _logger.DebugFormat("Created output message '{0}' for 'file://{1}/{2}/{3}' as NotReady", msgId, destination, queue, subQueue);
            return msgId;
        }

        public void MarkAsReadyToSend(Guid transactionId)
        {
            if (!Directory.Exists(_outgoingPath))
                return;
            var newExtension = OutgoingMessageStatus.Ready.ToString();
            foreach (var path in Directory.EnumerateFiles(_outgoingPath, FileUtil.SearchTransactionId(transactionId, null)))
            {
                FileUtil.MoveExtension(path, newExtension);
                _logger.DebugFormat("Marking output message {0} as Ready", transactionId);
            }
        }

        public void DeleteMessageToSend(Guid transactionId)
        {
            if (!Directory.Exists(_outgoingPath))
                return;
            foreach (var path in Directory.EnumerateFiles(_outgoingPath, FileUtil.SearchTransactionId(transactionId, null)))
            {
                _logger.DebugFormat("Deleting output message {0}", transactionId);
                File.Delete(path);
            }
        }

        public void MarkAllOutgoingInFlightMessagesAsReadyToSend()
        {
            if (!Directory.Exists(_outgoingPath))
                return;
            var newExtension = OutgoingMessageStatus.Ready.ToString();
            foreach (var path in Directory.EnumerateFiles(_outgoingPath, FileUtil.SearchTransactionId(Guid.Empty, OutgoingMessageStatus.InFlight.ToString())))
                FileUtil.MoveExtension(path, newExtension);
        }

        #endregion

        #region Recovery

        public void MarkAllProcessedMessagesWithTransactionsNotRegisterForRecoveryAsReadyToDeliver()
        {
            if (!Directory.Exists(_recoveryPath))
                return;

            var txsWithRecovery = new HashSet<Guid>();
            foreach (var x in Directory.EnumerateFiles(_recoveryPath))
                txsWithRecovery.Add(new Guid(x));

            var txsWithoutRecovery = new HashSet<Guid>();
            foreach (var x in Directory.EnumerateFiles(_txsPath))
                txsWithoutRecovery.Add(new Guid(x));

            foreach (var txId in txsWithoutRecovery)
            {
                if (txsWithRecovery.Contains(txId))
                    continue;
                ReverseAllFrom(txId);
            }
        }

        public void ReverseAllFrom(Guid transactionId)
        {
            if (!Directory.Exists(_txsPath))
                return;
            foreach (var x in Directory.EnumerateFiles(_txsPath, FileUtil.SearchTransactionId(transactionId, null)))
            {
                var obj = JsonConvert.DeserializeObject<Tx>(File.ReadAllText(x));
                var oldStatus = (MessageStatus)obj.valueToRestore;
                var subqueue = obj.subQueue;
                var actions = GetQueue(obj.queue);
                var bookmark = new MessageBookmark { Bookmark = obj.bookmark, QueueName = obj.queue };
                switch (actions.GetMessageStatus(bookmark))
                {
                    case MessageStatus.SubqueueChanged:
                        actions.SetMessageStatus(bookmark, MessageStatus.ReadyToDeliver, subqueue);
                        break;
                    case MessageStatus.EnqueueWait:
                        actions.Delete(bookmark);
                        break;
                    default:
                        actions.SetMessageStatus(bookmark, oldStatus);
                        break;
                }
            }
        }

        #endregion

        public string[] GetAllQueuesNames()
        {
            if (!Directory.Exists(_queuesPath))
                return new string[] { };
            return Directory.EnumerateFiles(_queuesPath).OrderBy(x => x)
                .ToArray();
        }

        #region Outgoing History

        public MessageBookmark GetSentMessageBookmarkAtPosition(int positionFromNewestSentMessage)
        {
            return Directory.EnumerateFiles(_outgoingHistoryPath).OrderByDescending(x => x)
                .Skip(positionFromNewestSentMessage)
                .Select(x => new MessageBookmark { Bookmark = x })
                .FirstOrDefault();
        }

        public IEnumerable<PersistentMessageToSend> GetSentMessages(int? batchSize = null)
        {
            var query = Directory.EnumerateFiles(_outgoingHistoryPath).OrderBy(x => x)
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
                        Bookmark = new MessageBookmark { Bookmark = x }
                    };
                });
            return (batchSize == null ? query : query.Take(batchSize.Value));
        }

        public void DeleteMessageToSendHistoric(MessageBookmark bookmark)
        {
            if (bookmark.Bookmark.StartsWith(_outgoingHistoryPath))
                File.Delete(bookmark.Bookmark);
        }

        #endregion

        public int GetNumberOfMessages(string queueName)
        {
            var path = Path.Combine(_database, queueName);
            if (!Directory.Exists(path))
                return -1;
            return Directory.GetFiles(path).Count();
        }

        #region Recieved

        public IEnumerable<MessageId> GetAlreadyReceivedMessageIds()
        {
            if (!Directory.Exists(_receivedPath))
                return Enumerable.Empty<MessageId>();
            return Directory.EnumerateFiles(_receivedPath).OrderBy(x => x)
                .Select(x => FileUtil.ToMessageId(x));
        }

        public void MarkReceived(MessageId id)
        {
            if (!Directory.Exists(_receivedPath))
                Directory.CreateDirectory(_receivedPath);
            var path = Path.Combine(_receivedPath, FileUtil.FromMessageId(id, null));
            File.WriteAllText(path, ".");
        }

        public IEnumerable<MessageId> DeleteOldestReceivedMessageIds(int numberOfItemsToKeep, int numberOfItemsToDelete)
        {
            return Directory.EnumerateFiles(_receivedPath).OrderByDescending(x => x)
                .Skip(numberOfItemsToKeep)
                .Take(numberOfItemsToDelete)
                .Select(x =>
                {
                    File.Delete(x);
                    return FileUtil.ToMessageId(x);
                });
        }

        #endregion
    }
}

