using Common.Logging;
using Rhino.Files.Model;
using System;
using System.Collections.Generic;

namespace Rhino.Files.Storage
{
    internal class GlobalActions : IDisposable
    {
        readonly QueueManagerConfiguration _configuration;
        readonly ILog _logger;

        public GlobalActions(string database, QueueManagerConfiguration configuration)
        {
            _logger = LogManager.GetLogger(typeof(GlobalActions));
            _configuration = configuration;
        }

        public void Dispose()
        {
        }

        public void RemoveReversalsMoveCompletedMessagesAndFinishSubQueueMove(Guid Id)
        {
        }

        public void MarkAsReadyToSend(Guid Id)
        {
        }

        public void DeleteRecoveryInformation(Guid Id)
        {
        }

        public void Commit()
        {
        }

        public void ReverseAllFrom(Guid Id)
        {
        }

        public void DeleteMessageToSend(Guid Id)
        {
        }

        public void RegisterRecoveryInformation(Guid Id, byte[] information)
        {
        }

        public IEnumerable<MessageId> GetAlreadyReceivedMessageIds()
        {
            return null;
        }

        public Guid RegisterToSend(string destination, string queue, string subqueue, MessagePayload payload, Guid guid)
        {
            return Guid.Empty;
        }

        public int GetNumberOfMessages(string queueName)
        {
            return 0;
        }

        public void CreateQueueIfDoesNotExists(string queueName)
        {
        }

        public QueueActions GetQueue(string p)
        {
            return null;
        }

        public void MarkReceived(Model.MessageId messageId)
        {
        }

        public void RegisterUpdateToReverse(Guid guid, MessageBookmark messageBookmark, MessageStatus messageStatus, string subqueue)
        {
        }

        public IEnumerable<PersistentMessageToSend> GetSentMessages()
        {
            return null;
        }

        internal MessageBookmark GetSentMessageBookmarkAtPosition(int numberOfMessagesToKeep)
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<PersistentMessageToSend> GetSentMessages(int batchSize)
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<MessageId> DeleteOldestReceivedMessageIds(int p, int numberOfItemsToDelete)
        {
            throw new NotImplementedException();
        }

        internal void MarkAllOutgoingInFlightMessagesAsReadyToSend()
        {
            throw new NotImplementedException();
        }

        internal void MarkAllProcessedMessagesWithTransactionsNotRegisterForRecoveryAsReadyToDeliver()
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<byte[]> GetRecoveryInformation()
        {
            throw new NotImplementedException();
        }

        internal string[] GetAllQueuesNames()
        {
            throw new NotImplementedException();
        }

        internal void DeleteMessageToSendHistoric(MessageBookmark messageBookmark)
        {
            throw new NotImplementedException();
        }
    }
}

