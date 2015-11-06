using Common.Logging;
using Rhino.Files.Model;
using System;
using System.Collections.Generic;

namespace Rhino.Files.Storage
{
    internal class QueueActions : IDisposable
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

        internal MessageBookmark GetMessageHistoryBookmarkAtPosition(int numberOfMessagesToKeep)
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<HistoryMessage> GetAllProcessedMessages(int batchSize = 100)
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<PersistentMessage> GetAllMessages(string subqueue)
        {
            throw new NotImplementedException();
        }

        internal MessageBookmark Enqueue(Message msg)
        {
            throw new NotImplementedException();
        }

        internal PersistentMessage Peek(string subqueue)
        {
            throw new NotImplementedException();
        }

        internal PersistentMessage Dequeue(string subqueue)
        {
            throw new NotImplementedException();
        }

        internal void SetMessageStatus(MessageBookmark bookmark, MessageStatus messageStatus)
        {
            throw new NotImplementedException();
        }

        internal void Discard(MessageBookmark bookmark)
        {
            throw new NotImplementedException();
        }

        internal MessageBookmark MoveTo(string subqueue, PersistentMessage persistentMessage)
        {
            throw new NotImplementedException();
        }

        public string[] Subqueues { get; set; }

        internal void DeleteHistoric(MessageBookmark messageBookmark)
        {
            throw new NotImplementedException();
        }

        internal PersistentMessage PeekById(MessageId id)
        {
            throw new NotImplementedException();
        }
    }
}

