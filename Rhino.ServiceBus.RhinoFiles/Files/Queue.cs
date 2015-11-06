using Rhino.Files.Model;
using System;

namespace Rhino.Files
{
    public interface IQueue
    {
        string Endpoint { get; }
        string QueueName { get; }

        void EnqueueDirectlyTo(string subqueue, MessagePayload payload);
        PersistentMessage[] GetAllMessages(string subqueue);
        HistoryMessage[] GetAllProcessedMessages();
        string[] GetSubqeueues();
        void MoveTo(string subqueue, Message message);
        Message Peek();
        Message Peek(string subqueue);
        Message Peek(TimeSpan timeout);
        Message Peek(string subqueue, TimeSpan timeout);
        Message PeekById(MessageId id);
        Message Receive();
        Message Receive(string subqueue);
        Message Receive(TimeSpan timeout);
        Message Receive(string subqueue, TimeSpan timeout);
    }

    internal class Queue : IQueue
    {
        readonly IQueueManager _queueManager;
        readonly string _queueName;

        public Queue(IQueueManager queueManager, string queueName)
        {
            _queueManager = queueManager;
            _queueName = queueName;
        }

        public string Endpoint
        {
            get { return _queueManager.Endpoint; }
        }

        public string QueueName
        {
            get { return _queueName; }
        }

        public void EnqueueDirectlyTo(string subqueue, MessagePayload payload) { _queueManager.EnqueueDirectlyTo(_queueName, subqueue, payload); }
        public PersistentMessage[] GetAllMessages(string subqueue) { return _queueManager.GetAllMessages(_queueName, subqueue); }
        public HistoryMessage[] GetAllProcessedMessages() { return _queueManager.GetAllProcessedMessages(_queueName); }
        public string[] GetSubqeueues() { return _queueManager.GetSubqueues(_queueName); }
        public void MoveTo(string subqueue, Message message) { _queueManager.MoveTo(subqueue, message); }
        public Message Peek() { return _queueManager.Peek(_queueName); }
        public Message Peek(string subqueue) { return _queueManager.Peek(_queueName, subqueue); }
        public Message Peek(TimeSpan timeout) { return _queueManager.Peek(_queueName, timeout); }
        public Message Peek(string subqueue, TimeSpan timeout) { return _queueManager.Peek(_queueName, subqueue, timeout); }
        public Message PeekById(MessageId id) { return _queueManager.PeekById(_queueName, id); }
        public Message Receive() { return _queueManager.Receive(_queueName); }
        public Message Receive(string subqueue) { return _queueManager.Receive(_queueName, subqueue); }
        public Message Receive(TimeSpan timeout) { return Receive(_queueName, timeout); }
        public Message Receive(string subqueue, TimeSpan timeout) { return _queueManager.Receive(_queueName, subqueue, timeout); }
    }
}