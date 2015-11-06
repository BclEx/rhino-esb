using Common.Logging;
using Rhino.Files.Model;
using System;
using System.Collections.Generic;

namespace Rhino.Files.Storage
{
    public class QueueActions : IDisposable
    {
        readonly QueueManagerConfiguration _configuration;
        readonly ILog _logger;

        public QueueActions(string database, QueueManagerConfiguration configuration)
        {
            _logger = LogManager.GetLogger(typeof(GlobalActions));
            _configuration = configuration;
        }

        public void Dispose()
        {
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

        public MessageBookmark Enqueue(Message msg)
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

        public string[] Subqueues { get; set; }

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

