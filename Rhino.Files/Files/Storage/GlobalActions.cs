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
            public DateTime timestamp { get; set; }
            public byte[] data { get; set; }
            public Guid instanceId { get; set; }
            public Guid msgId { get; set; }
            public string subqueue { get; set; }
            public string headers { get; set; }
        }

        public void CreateQueueIfDoesNotExists(string queueName)
        {
            if (Directory.Exists(_database))
                Directory.CreateDirectory(_database);
            var path = Path.Combine(_database, queueName);
            if (Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        #endregion

        #region Recovery

        public void RegisterRecoveryInformation(Guid transactionId, byte[] information)
        {
            if (Directory.Exists(_recoveryPath))
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
                .OrderBy(x => x)
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
            public string subqueue { get; set; }
        }

        public void RegisterUpdateToReverse(Guid txId, MessageBookmark bookmark, MessageStatus statusToRestore, string subQueue)
        {
            if (Directory.Exists(_txsPath))
                Directory.CreateDirectory(_txsPath);
            var path = Path.Combine(_txsPath, bookmark.Bookmark);
            if (File.Exists(path))
                File.Delete(path);
            var obj = new Tx
            {
                txId = txId,
                bookmark = bookmark.Bookmark,
                valueToRestore = (int)statusToRestore,
                queue = bookmark.QueueName,
                subqueue = subQueue,
            };
            File.WriteAllText(path, JsonConvert.SerializeObject(obj));
        }

        public void RemoveReversalsMoveCompletedMessagesAndFinishSubQueueMove(Guid transactionId)
        {
            //Api.JetSetCurrentIndex(session, txs, "by_tx_id");
            //Api.MakeKey(session, txs, transactionId.ToByteArray(), MakeKeyGrbit.NewKey);

            //if (Api.TrySeek(session, txs, SeekGrbit.SeekEQ) == false)
            //    return;
            //Api.MakeKey(session, txs, transactionId.ToByteArray(), MakeKeyGrbit.NewKey);
            //try
            //{
            //    Api.JetSetIndexRange(session, txs, SetIndexRangeGrbit.RangeInclusive | SetIndexRangeGrbit.RangeUpperLimit);
            //}
            //catch (EsentErrorException e)
            //{
            //    if (e.Error != JET_err.NoCurrentRecord)
            //        throw;
            //    return;
            //}

            //do
            //{
            //    var queue = Api.RetrieveColumnAsString(session, txs, ColumnsInformation.TxsColumns["queue"], Encoding.Unicode);
            //    var bookmarkData = Api.RetrieveColumn(session, txs, ColumnsInformation.TxsColumns["bookmark_data"]);
            //    var bookmarkSize = Api.RetrieveColumnAsInt32(session, txs, ColumnsInformation.TxsColumns["bookmark_size"]).Value;

            //    var actions = GetQueue(queue);

            //    var bookmark = new MessageBookmark
            //    {
            //        Bookmark = bookmarkData,
            //        QueueName = queue,
            //        Size = bookmarkSize
            //    };

            //    switch (actions.GetMessageStatus(bookmark))
            //    {
            //        case MessageStatus.SubqueueChanged:
            //        case MessageStatus.EnqueueWait:
            //            actions.SetMessageStatus(bookmark, MessageStatus.ReadyToDeliver);
            //            break;
            //        default:
            //            if (configuration.EnableProcessedMessageHistory)
            //                actions.MoveToHistory(bookmark);
            //            else
            //                actions.Delete(bookmark);
            //            break;
            //    }

            //    Api.JetDelete(session, txs);
            //} while (Api.TryMoveNext(session, txs));
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
            if (Directory.Exists(_outgoingPath))
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
            var path = Path.Combine(_outgoingPath, transactionId.ToString() + "." + OutgoingMessageStatus.NotReady.ToString());
            using (var s = File.CreateText(path))
            {
                s.Write(JsonConvert.SerializeObject(obj));
                s.Flush();
            }
            var bookmark = new MessageBookmark { Bookmark = path };
            _outgoingBookmark = bookmark;
            _logger.DebugFormat("Created output message '{0}' for 'file://{1}/{2}/{3}' as NotReady", msgId, destination, queue, subQueue);
            return msgId;
        }

        public void MarkAsReadyToSend(Guid transactionId)
        {
            if (!Directory.Exists(_outgoingPath))
                return;
            var newExtension = "." + OutgoingMessageStatus.Ready.ToString();
            var paths = Directory.EnumerateFiles(_outgoingPath, transactionId.ToString() + ".*");
            foreach (var path in paths)
            {
                var newPath = path.Substring(0, path.Length - Path.GetExtension(path).Length) + newExtension;
                File.Move(path, newPath);
                _logger.DebugFormat("Marking output message {0} as Ready", transactionId);
            }
        }

        public void DeleteMessageToSend(Guid transactionId)
        {
            if (!Directory.Exists(_outgoingPath))
                return;
            var paths = Directory.EnumerateFiles(_outgoingPath, transactionId.ToString() + ".*");
            foreach (var path in paths)
            {
                _logger.DebugFormat("Deleting output message {0}", transactionId);
                File.Delete(path);
            }
        }

        public void MarkAllOutgoingInFlightMessagesAsReadyToSend()
        {
            if (!Directory.Exists(_outgoingPath))
                return;
            var newExtension = "." + OutgoingMessageStatus.Ready.ToString();
            var paths = Directory.EnumerateFiles(_outgoingPath, "*." + OutgoingMessageStatus.InFlight.ToString());
            foreach (var path in paths)
            {
                var newPath = path.Substring(0, path.Length - Path.GetExtension(path).Length) + newExtension;
                File.Move(path, newPath);
            }
        }

        #endregion

        #region Recovery

        public void MarkAllProcessedMessagesWithTransactionsNotRegisterForRecoveryAsReadyToDeliver()
        {
            if (!Directory.Exists(_recoveryPath))
                return;

            //var txsWithRecovery = new HashSet<Guid>();
            //Api.MoveBeforeFirst(session, recovery);
            //while (Api.TryMoveNext(session, recovery))
            //{
            //    var idAsBytes = Api.RetrieveColumn(session, recovery, ColumnsInformation.RecoveryColumns["tx_id"]);
            //    txsWithRecovery.Add(new Guid(idAsBytes));
            //}

            //var txsWithoutRecovery = new HashSet<Guid>();
            //Api.MoveBeforeFirst(session, txs);
            //while (Api.TryMoveNext(session, txs))
            //{
            //    var idAsBytes = Api.RetrieveColumn(session, txs, ColumnsInformation.RecoveryColumns["tx_id"]);
            //    txsWithoutRecovery.Add(new Guid(idAsBytes));
            //}

            //foreach (var txId in txsWithoutRecovery)
            //{
            //    if (txsWithRecovery.Contains(txId))
            //        continue;
            //    ReverseAllFrom(txId);
            //}
        }

        public void ReverseAllFrom(Guid transactionId)
        {
            //Api.JetSetCurrentIndex(session, txs, "by_tx_id");
            //Api.MakeKey(session, txs, transactionId.ToByteArray(), MakeKeyGrbit.NewKey);

            //if (Api.TrySeek(session, txs, SeekGrbit.SeekEQ) == false)
            //    return;

            //Api.MakeKey(session, txs, transactionId.ToByteArray(), MakeKeyGrbit.NewKey);
            //try
            //{
            //    Api.JetSetIndexRange(session, txs, SetIndexRangeGrbit.RangeUpperLimit | SetIndexRangeGrbit.RangeInclusive);
            //}
            //catch (EsentErrorException e)
            //{
            //    if (e.Error != JET_err.NoCurrentRecord)
            //        throw;
            //    return;
            //}

            //do
            //{
            //    var bytes = Api.RetrieveColumn(session, txs, ColumnsInformation.TxsColumns["bookmark_data"]);
            //    var size = Api.RetrieveColumnAsInt32(session, txs, ColumnsInformation.TxsColumns["bookmark_size"]).Value;
            //    var oldStatus = (MessageStatus)Api.RetrieveColumnAsInt32(session, txs, ColumnsInformation.TxsColumns["value_to_restore"]).Value;
            //    var queue = Api.RetrieveColumnAsString(session, txs, ColumnsInformation.TxsColumns["queue"]);
            //    var subqueue = Api.RetrieveColumnAsString(session, txs, ColumnsInformation.TxsColumns["subqueue"]);

            //    var bookmark = new MessageBookmark
            //    {
            //        QueueName = queue,
            //        Bookmark = bytes,
            //        Size = size
            //    };
            //    var actions = GetQueue(queue);
            //    var newStatus = actions.GetMessageStatus(bookmark);
            //    switch (newStatus)
            //    {
            //        case MessageStatus.SubqueueChanged:
            //            actions.SetMessageStatus(bookmark, MessageStatus.ReadyToDeliver, subqueue);
            //            break;
            //        case MessageStatus.EnqueueWait:
            //            actions.Delete(bookmark);
            //            break;
            //        default:
            //            actions.SetMessageStatus(bookmark, oldStatus);
            //            break;
            //    }
            //} while (Api.TryMoveNext(session, txs));
        }

        #endregion

        public string[] GetAllQueuesNames()
        {
            if (!Directory.Exists(_queuesPath))
                return new string[] { };
            return Directory.EnumerateFiles(_queuesPath)
                .OrderBy(x => x)
                .ToArray();
        }

        #region Outgoing History

        public MessageBookmark GetSentMessageBookmarkAtPosition(int positionFromNewestSentMessage)
        {
            return Directory.EnumerateFiles(_outgoingHistoryPath, "*.*")
                .OrderByDescending(x => x)
                .Skip(positionFromNewestSentMessage)
                .Select(x => new MessageBookmark { Bookmark = x })
                .FirstOrDefault();
        }

        public IEnumerable<PersistentMessageToSend> GetSentMessages(int? batchSize = null)
        {
            return null;
            //Api.MoveBeforeFirst(session, _outgoingHistoryPath);

            //int count = 0;
            //while (Api.TryMoveNext(session, outgoingHistory) && count++ != batchSize)
            //{
            //    var address = Api.RetrieveColumnAsString(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["address"]);
            //    var port = Api.RetrieveColumnAsInt32(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["port"]).Value;

            //    var bookmark = new MessageBookmark();
            //    Api.JetGetBookmark(session, outgoingHistory, bookmark.Bookmark, bookmark.Size, out bookmark.Size);

            //    yield return new PersistentMessageToSend
            //    {
            //        Id = new MessageId
            //        {
            //            SourceInstanceId = instanceId,
            //            MessageIdentifier = new Guid(Api.RetrieveColumn(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["msg_id"]))
            //        },
            //        OutgoingStatus = (OutgoingMessageStatus)Api.RetrieveColumnAsInt32(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["send_status"]).Value,
            //        Endpoint = new Endpoint(address, port),
            //        Queue = Api.RetrieveColumnAsString(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["queue"], Encoding.Unicode),
            //        SubQueue = Api.RetrieveColumnAsString(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["subqueue"], Encoding.Unicode),
            //        SentAt = DateTime.FromOADate(Api.RetrieveColumnAsDouble(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["sent_at"]).Value),
            //        Data = Api.RetrieveColumn(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["data"]),
            //        Bookmark = bookmark
            //    };
            //}
        }

        public void DeleteMessageToSendHistoric(MessageBookmark bookmark)
        {
            //Api.JetGotoBookmark(session, _outgoingHistoryPath, bookmark.Bookmark, bookmark.Size);
            //Api.JetDelete(session, outgoingHistory);
        }

        #endregion

        public int GetNumberOfMessages(string queueName)
        {
            //Api.JetSetCurrentIndex(session, queues, "pk");
            //Api.MakeKey(session, queues, queueName, Encoding.Unicode, MakeKeyGrbit.NewKey);

            //if (Api.TrySeek(session, queues, SeekGrbit.SeekEQ) == false)
            //    return -1;

            var bytes = new byte[4];
            //var zero = BitConverter.GetBytes(0);
            //int actual;
            //Api.JetEscrowUpdate(session, queues, ColumnsInformation.QueuesColumns["number_of_messages"], zero, zero.Length, bytes, bytes.Length, out actual, EscrowUpdateGrbit.None);
            return BitConverter.ToInt32(bytes, 0);
        }

        #region Recieved

        public IEnumerable<MessageId> GetAlreadyReceivedMessageIds()
        {
            return Directory.EnumerateFiles(_receivedPath, "*")
                .OrderBy(x => x)
                .Select(x =>
                {
                    var parts = Path.GetFileNameWithoutExtension(x).Split('+');
                    return new MessageId
                    {
                        SourceInstanceId = new Guid(parts[1]),
                        MessageIdentifier = new Guid(parts[2]),
                    };
                });
        }

        public void MarkReceived(MessageId id)
        {
            if (Directory.Exists(_receivedPath))
                Directory.CreateDirectory(_receivedPath);
            var path = Path.Combine(_recoveryPath, DateTime.Now.Ticks.ToString("X16") + "+" + id.SourceInstanceId.ToString() + "+" + id.MessageIdentifier.ToString());
            using (var s = File.CreateText(path))
            {
                s.Write("NA");
                s.Flush();
            }
        }

        public IEnumerable<MessageId> DeleteOldestReceivedMessageIds(int numberOfItemsToKeep, int numberOfItemsToDelete)
        {
            return Directory.EnumerateFiles(_receivedPath, "*")
                .OrderByDescending(x => x)
                .Skip(numberOfItemsToKeep)
                .Take(numberOfItemsToDelete)
                .Select(x =>
                {
                    File.Delete(x);
                    var parts = Path.GetFileNameWithoutExtension(x).Split('+');
                    return new MessageId
                    {
                        SourceInstanceId = new Guid(parts[1]),
                        MessageIdentifier = new Guid(parts[2]),
                    };
                });
        }

        #endregion

    }
}

