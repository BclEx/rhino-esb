using Rhino.Files.Model;
using Rhino.Files.Protocol;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Rhino.Files.Tests
{
    public static class ObjectMother
    {
        public static Message[] MessageBatchSingleMessage(string message = null, string queueName = null)
        {
            return new[] { SingleMessage() };
        }

        public static Message SingleMessage(string message = "hello", string queueName = "h")
        {
            return new Message
            {
                Id = MessageId.GenerateRandom(),
                Queue = queueName,
                Data = Encoding.Unicode.GetBytes(message),
                SentAt = DateTime.Now
            };
        }

        public static Sender Sender()
        {
            return new Sender
            {
                Destination = "localhost",
                Failure = exception => Assert.False(true),
                Success = () => null,
                Messages = MessageBatchSingleMessage(),
            };
        }

        public static Uri UriFor(string queue = "h")
        {
            return new Uri(string.Format("file://localhost/{0}", queue));
        }

        public static MessagePayload MessagePayload()
        {
            return new MessagePayload
            {
                Data = Encoding.UTF8.GetBytes("hello")
            };
        }

        public static QueueManager QueueManager(string name = "test", string queue = "h")
        {
            var directory = string.Format("{0}.esent", name);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
            var queueManager = new QueueManager("Loopback", directory);
            queueManager.CreateQueues(queue);
            return queueManager;
        }
    }
}