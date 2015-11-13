using Common.Logging;
using Rhino.Files.Model;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Rhino.Files.Storage
{
    public class QueueActions : AbstractActions
    {
        readonly ILog _logger = LogManager.GetLogger(typeof(GlobalActions));
        readonly string _queueName;
        string[] _subqueues;
        readonly AbstractActions _actions;
        readonly Action<int> _changeNumberOfMessages;

        public QueueActions(string database, string queueName, string[] subqueues, AbstractActions actions, Action<int> changeNumberOfMessages)
            : base(database)
        {
            _queueName = queueName;
            _subqueues = subqueues;
            _actions = actions;
            _changeNumberOfMessages = changeNumberOfMessages;
        }

        public string[] Subqueues
        {
            get { return _subqueues; }
        }

        public MessageBookmark Enqueue(Message message)
        {
            var bm = new MessageBookmark { QueueName = _queueName };
            //using (var updateMsgs = new Update(session, msgs, JET_prep.Insert))
            //{
            //    var messageStatus = MessageStatus.InTransit;
            //    var persistentMessage = message as PersistentMessage;
            //    if (persistentMessage != null)
            //        messageStatus = persistentMessage.Status;

            //    Api.SetColumn(session, msgs, msgsColumns["timestamp"], message.SentAt.ToOADate());
            //    Api.SetColumn(session, msgs, msgsColumns["data"], message.Data);
            //    Api.SetColumn(session, msgs, msgsColumns["instance_id"], message.Id.SourceInstanceId.ToByteArray());
            //    Api.SetColumn(session, msgs, msgsColumns["msg_id"], message.Id.MessageIdentifier.ToByteArray());
            //    Api.SetColumn(session, msgs, msgsColumns["subqueue"], message.SubQueue, Encoding.Unicode);
            //    Api.SetColumn(session, msgs, msgsColumns["headers"], message.Headers.ToQueryString(), Encoding.Unicode);
            //    Api.SetColumn(session, msgs, msgsColumns["status"], (int)messageStatus);

            //    updateMsgs.Save(bm.Bookmark, bm.Size, out bm.Size);
            //}
            if (!string.IsNullOrEmpty(message.SubQueue) && !Subqueues.Contains(message.SubQueue))
            {
                _actions.AddSubqueueTo(_queueName, message.SubQueue);
                _subqueues = _subqueues.Union(new[] { message.SubQueue }).ToArray();
            }

            _logger.DebugFormat("Enqueuing msg to '{0}' with subqueue: '{1}'. Id: {2}", _queueName, message.SubQueue, message.Id);
            _changeNumberOfMessages(1);
            return bm;
        }

        public MessageBookmark GetMessageHistoryBookmarkAtPosition(int numberOfMessagesToKeep)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HistoryMessage> GetAllProcessedMessages(int batchSize = 100)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<PersistentMessage> GetAllMessages(string subqueue)
        {
            throw new NotImplementedException();
        }

        public PersistentMessage Peek(string subqueue)
        {
            throw new NotImplementedException();
        }

        public PersistentMessage Dequeue(string subqueue)
        {
            throw new NotImplementedException();
        }

        public void SetMessageStatus(MessageBookmark bookmark, MessageStatus messageStatus)
        {
            throw new NotImplementedException();
        }

        public void Discard(MessageBookmark bookmark)
        {
            throw new NotImplementedException();
        }

        public MessageBookmark MoveTo(string subqueue, PersistentMessage persistentMessage)
        {
            throw new NotImplementedException();
        }

        public void DeleteHistoric(MessageBookmark messageBookmark)
        {
            throw new NotImplementedException();
        }

        public PersistentMessage PeekById(MessageId id)
        {
            throw new NotImplementedException();
        }
    }
}

