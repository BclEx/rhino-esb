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

            var items = Directory.EnumerateFiles(_outgoingPath)
                .OrderBy(x => x)
                .Select(x => new Tuple<string, Outgoing>(x, JsonConvert.DeserializeObject<Outgoing>(File.ReadAllText(x))));
            foreach (var item in items)
            {
                var path = item.Item1;
                var obj = item.Item2;
                var msgId = obj.msgId;
                var value = (OutgoingMessageStatus)Enum.Parse(typeof(OutgoingMessageStatus), Path.GetExtension(path));
                var time = obj.timeToSend;

                _logger.DebugFormat("Scanning message {0} with status {1} to be sent at {2}", msgId, value, time);
                if (value != OutgoingMessageStatus.Ready)
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

                var bookmark = new MessageBookmark { }; //Bookmark = obj.bookmark };

                _logger.DebugFormat("Adding message {0} to returned messages", msgId);
                var headerAsQueryString = obj.headers;
                messages.Add(new PersistentMessage
                {
                    Id = new MessageId
                    {
                        SourceInstanceId = _instanceId,
                        MessageIdentifier = msgId
                    },
                    Headers = HttpUtility.ParseQueryString(headerAsQueryString),
                    Queue = rowQueue,
                    SubQueue = obj.subqueue,
                    SentAt = obj.sentAt,
                    Data = obj.data,
                    Bookmark = bookmark
                });

                var newExtension = "." + OutgoingMessageStatus.InFlight.ToString();
                var newPath = path.Substring(0, path.Length - Path.GetExtension(path).Length) + newExtension;
                File.Move(path, newPath);

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
            //Api.JetGotoBookmark(session, outgoing, bookmark.Bookmark, bookmark.Size);
            //var numOfRetries = Api.RetrieveColumnAsInt32(session, outgoing, ColumnsInformation.OutgoingColumns["number_of_retries"]).Value;
            //var msgId = new Guid(Api.RetrieveColumn(session, outgoing, ColumnsInformation.OutgoingColumns["msg_id"]));

            //if (numOfRetries < 100 && queueDoesNotExistsInDestination == false)
            //{
            //    using (var update = new Update(session, outgoing, JET_prep.Replace))
            //    {
            //        var timeToSend = DateTime.Now.AddSeconds(numOfRetries * numOfRetries);


            //        Api.SetColumn(session, outgoing, ColumnsInformation.OutgoingColumns["send_status"], (int)OutgoingMessageStatus.Ready);
            //        Api.SetColumn(session, outgoing, ColumnsInformation.OutgoingColumns["time_to_send"],
            //                      timeToSend.ToOADate());
            //        Api.SetColumn(session, outgoing, ColumnsInformation.OutgoingColumns["number_of_retries"],
            //                      numOfRetries + 1);

            //        logger.DebugFormat("Marking outgoing message {0} as failed with retries: {1}",
            //                           msgId, numOfRetries);

            //        update.Save();
            //    }
            //}
            //else
            //{
            //    MoveFailedMessageToOutgoingHistory(numOfRetries, msgId);
            //}
        }

        public MessageBookmark MarkOutgoingMessageAsSuccessfullySent(MessageBookmark bookmark)
        {
            //Api.JetGotoBookmark(session, outgoing, bookmark.Bookmark, bookmark.Size);
            var newBookmark = new MessageBookmark();
            //using (var update = new Update(session, _outgoingHistoryPath, JET_prep.Insert))
            //{
            //    foreach (var column in ColumnsInformation.OutgoingColumns.Keys)
            //    {
            //        var bytes = Api.RetrieveColumn(session, outgoing, ColumnsInformation.OutgoingColumns[column]);
            //        Api.SetColumn(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns[column], bytes);
            //    }
            //    Api.SetColumn(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["send_status"], (int)OutgoingMessageStatus.Sent);

            //    update.Save(newBookmark.Bookmark, newBookmark.Size, out newBookmark.Size);
            //}
            //var msgId = new Guid(Api.RetrieveColumn(session, outgoing, ColumnsInformation.OutgoingColumns["msg_id"]));
            //Api.JetDelete(session, outgoing);
            //_logger.DebugFormat("Successfully sent output message {0}", msgId);
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
            return null;
            //Api.MoveBeforeFirst(session, outgoing);

            //while (Api.TryMoveNext(session, outgoing))
            //{
            //    var address = Api.RetrieveColumnAsString(session, outgoing, ColumnsInformation.OutgoingColumns["address"]);
            //    var port = Api.RetrieveColumnAsInt32(session, outgoing, ColumnsInformation.OutgoingColumns["port"]).Value;

            //    var bookmark = new MessageBookmark();
            //    Api.JetGetBookmark(session, outgoing, bookmark.Bookmark, bookmark.Size, out bookmark.Size);

            //    yield return new PersistentMessageToSend
            //    {
            //        Id = new MessageId
            //        {
            //            SourceInstanceId = instanceId,
            //            MessageIdentifier = new Guid(Api.RetrieveColumn(session, outgoing, ColumnsInformation.OutgoingColumns["msg_id"]))
            //        },
            //        OutgoingStatus = (OutgoingMessageStatus)Api.RetrieveColumnAsInt32(session, outgoing, ColumnsInformation.OutgoingColumns["send_status"]).Value,
            //        Endpoint = new Endpoint(address, port),
            //        Queue = Api.RetrieveColumnAsString(session, outgoing, ColumnsInformation.OutgoingColumns["queue"], Encoding.Unicode),
            //        SubQueue = Api.RetrieveColumnAsString(session, outgoing, ColumnsInformation.OutgoingColumns["subqueue"], Encoding.Unicode),
            //        SentAt = DateTime.FromOADate(Api.RetrieveColumnAsDouble(session, outgoing, ColumnsInformation.OutgoingColumns["sent_at"]).Value),
            //        Data = Api.RetrieveColumn(session, outgoing, ColumnsInformation.OutgoingColumns["data"]),
            //        Bookmark = bookmark
            //    };
            //}
        }

        public void RevertBackToSend(MessageBookmark[] bookmarks)
        {
            //foreach (var bookmark in bookmarks)
            //{
            //    Api.JetGotoBookmark(session, _outgoingHistoryPath, bookmark.Bookmark, bookmark.Size);
            //    var msgId = new Guid(Api.RetrieveColumn(session, _outgoingHistoryPath, ColumnsInformation.OutgoingColumns["msg_id"]));

            //    using (var update = new Update(session, outgoing, JET_prep.Insert))
            //    {
            //        foreach (var column in ColumnsInformation.OutgoingColumns.Keys)
            //        {
            //            Api.SetColumn(session, outgoing, ColumnsInformation.OutgoingColumns[column],
            //                Api.RetrieveColumn(session, _outgoingHistoryPath, ColumnsInformation.OutgoingHistoryColumns[column])
            //                );
            //        }
            //        Api.SetColumn(session, outgoing, ColumnsInformation.OutgoingColumns["send_status"],
            //            (int)OutgoingMessageStatus.Ready);
            //        Api.SetColumn(session, outgoing, ColumnsInformation.OutgoingColumns["number_of_retries"],
            //            Api.RetrieveColumnAsInt32(session, outgoingHistory, ColumnsInformation.OutgoingHistoryColumns["number_of_retries"]).Value + 1
            //               );

            //        logger.DebugFormat("Reverting output message {0} back to Ready mode", msgId);

            //        update.Save();
            //    }
            //    Api.JetDelete(session, _outgoingHistoryPath);
            //}
        }

        private void MoveFailedMessageToOutgoingHistory(string path, int numOfRetries, Guid msgId)
        {
            if (_configuration.EnableOutgoingMessageHistory)
            {
                var newExtension = "." + OutgoingMessageStatus.Failed.ToString();
                var newPath = _outgoingHistoryPath + Path.GetFileNameWithoutExtension(path) + newExtension;
                File.Move(path, newPath);
                _logger.DebugFormat("Marking outgoing message {0} as permenantly failed after {1} retries", msgId, numOfRetries);
            }
            else
                File.Delete(path);
        }
    }
}

