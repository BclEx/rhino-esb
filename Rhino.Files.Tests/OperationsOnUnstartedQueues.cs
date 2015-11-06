using Rhino.Files.Tests.Protocol;
using System;
using System.IO;
using System.Threading;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests
{
    public class OperationsOnUnstartedQueues : WithDebugging, IDisposable
    {
        QueueManager _sender, _receiver;

        public void SetupReceivedMessages()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            if (Directory.Exists("test2.esent"))
                Directory.Delete("test2.esent", true);
            _sender = new QueueManager("Loopback", "test.esent");
            _sender.Start();
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("files://localhost:23457/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 6, 7, 8, 9 }
                    });
                tx.Complete();
            }
            _receiver = new QueueManager("Loopback", "test2.esent");
            _receiver.CreateQueues("h", "a");
            var wait = new ManualResetEvent(false);
            Action<object, MessageEventArgs> handler = (s, e) => wait.Set();
            _receiver.MessageQueuedForReceive += handler;
            _receiver.Start();

            wait.WaitOne();

            _receiver.MessageQueuedForReceive -= handler;
            _sender.Dispose();
            _receiver.Dispose();

            _sender = new QueueManager("Loopback", "test.esent");
            _receiver = new QueueManager("Loopback", "test2.esent");
            _receiver.CreateQueues("h", "a");
        }

        private void SetupUnstarted()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            if (Directory.Exists("test2.esent"))
                Directory.Delete("test2.esent", true);
            _sender = new QueueManager("Loopback", "test.esent");
            _receiver = new QueueManager("Loopback", "test2.esent");
            _receiver.CreateQueues("h", "a");
        }

        [Fact]
        public void Can_send_before_start()
        {
            SetupUnstarted();
            _receiver.Start();
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("rhino.queues://localhost:23457/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 1, 2, 4, 5 }
                    });
                tx.Complete();
            }
            _sender.Start();
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", null, TimeSpan.FromSeconds(5));
                Assert.Equal(new byte[] { 1, 2, 4, 5 }, message.Data);
                tx.Complete();
            }
        }

        [Fact]
        public void Can_look_at_sent_messages_without_starting()
        {
            SetupReceivedMessages();
            var messages = _sender.GetAllSentMessages();
            Assert.Equal(1, messages.Length);
            Assert.Equal(new byte[] { 6, 7, 8, 9 }, messages[0].Data);
        }

        [Fact]
        public void Can_look_at_messages_queued_for_send_without_starting()
        {
            SetupUnstarted();
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 1, 2, 4, 5 }
                    });
                tx.Complete();
            }
            var messages = _sender.GetMessagesCurrentlySending();
            Assert.Equal(1, messages.Length);
            Assert.Equal(new byte[] { 1, 2, 4, 5 }, messages[0].Data);
        }

        [Fact]
        public void Can_receive_queued_messages_without_starting()
        {
            SetupReceivedMessages();
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", null, TimeSpan.FromSeconds(5));
                Assert.Equal(new byte[] { 6, 7, 8, 9 }, message.Data);
                tx.Complete();
            }
        }

        [Fact]
        public void Can_peek_queued_messages_without_starting()
        {
            SetupReceivedMessages();
            var message = _receiver.Peek("h", null, TimeSpan.FromSeconds(5));
            Assert.Equal(new byte[] { 6, 7, 8, 9 }, message.Data);
        }

        [Fact]
        public void Can_move_queued_messages_without_starting()
        {
            SetupReceivedMessages();
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Peek("h", null, TimeSpan.FromSeconds(5));
                _receiver.MoveTo("b", message);
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", "b");
                Assert.Equal(new byte[] { 6, 7, 8, 9 }, message.Data);
                tx.Complete();
            }
        }

        [Fact]
        public void Can_directly_enqueue_messages_without_starting()
        {
            SetupUnstarted();
            using (var tx = new TransactionScope())
            {
                _receiver.EnqueueDirectlyTo("h", null, new MessagePayload { Data = new byte[] { 8, 6, 4, 2 } });
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h");
                Assert.Equal(new byte[] { 8, 6, 4, 2 }, message.Data);
                tx.Complete();
            }
        }

        [Fact]
        public void Can_get_number_of_messages_without_starting()
        {
            SetupReceivedMessages();
            var numberOfMessagesQueuedForReceive = _receiver.GetNumberOfMessages("h");
            Assert.Equal(1, numberOfMessagesQueuedForReceive);
        }

        [Fact]
        public void Can_get_messages_without_starting()
        {
            SetupReceivedMessages();
            var messagesQueuedForReceive = _receiver.GetAllMessages("h", null);
            Assert.Equal(1, messagesQueuedForReceive.Length);
            Assert.Equal(new byte[] { 6, 7, 8, 9 }, messagesQueuedForReceive[0].Data);
        }

        [Fact]
        public void Can_get_processed_messages_without_starting()
        {
            SetupReceivedMessages();
            using (var tx = new TransactionScope())
            {
                _receiver.Receive("h");
                tx.Complete();
            }
            var processedMessages = _receiver.GetAllProcessedMessages("h");
            Assert.Equal(1, processedMessages.Length);
            Assert.Equal(new byte[] { 6, 7, 8, 9 }, processedMessages[0].Data);
        }

        [Fact]
        public void Can_get_Queues_without_starting()
        {
            SetupUnstarted();
            Assert.Equal(2, _receiver.Queues.Length);
        }

        [Fact]
        public void Can_get_Subqueues_without_starting()
        {
            SetupUnstarted();
            using (var tx = new TransactionScope())
            {
                _receiver.EnqueueDirectlyTo("h", "a", new MessagePayload { Data = new byte[] { 8, 6, 4, 2 } });
                _receiver.EnqueueDirectlyTo("h", "b", new MessagePayload { Data = new byte[] { 8, 6, 4, 2 } });
                _receiver.EnqueueDirectlyTo("h", "c", new MessagePayload { Data = new byte[] { 8, 6, 4, 2 } });
                tx.Complete();
            }
            Assert.Equal(3, _receiver.GetSubqueues("h").Length);
        }

        [Fact]
        public void Can_get_Queue_without_starting()
        {
            SetupUnstarted();
            Assert.NotNull(_receiver.GetQueue("h"));
        }

        public void Dispose()
        {
            _sender.Dispose();
            _receiver.Dispose();
        }
    }
}
