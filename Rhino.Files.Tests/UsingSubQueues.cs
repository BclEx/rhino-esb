using Rhino.Files.Tests.Protocol;
using System;
using System.IO;
using System.Text;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests
{
    public class UsingSubQueues : WithDebugging, IDisposable
    {
        readonly QueueManager _sender, _receiver;

        public UsingSubQueues()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            if (Directory.Exists("test2.esent"))
                Directory.Delete("test2.esent", true);
            _sender = new QueueManager("Loopback", "test.esent");
            _sender.Start();
            _receiver = new QueueManager("Loopback", "test2.esent");
            _receiver.CreateQueues("h", "a");
            _receiver.Start();
        }

        [Fact]
        public void Can_send_and_receive_subqueue()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h/a"),
                    new MessagePayload
                    {
                        Data = Encoding.Unicode.GetBytes("subzero")
                    });
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", "a");
                Assert.Equal("subzero", Encoding.Unicode.GetString(message.Data));
                tx.Complete();
            }
        }

        [Fact]
        public void Can_remove_and_move_msg_to_subqueue()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = Encoding.Unicode.GetBytes("subzero")
                    });

                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h");
                _receiver.MoveTo("b", message);
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", "b");
                Assert.Equal("subzero", Encoding.Unicode.GetString(message.Data));
                tx.Complete();
            }
        }

        [Fact]
        public void Can_peek_and_move_msg_to_subqueue()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = Encoding.Unicode.GetBytes("subzero")
                    });
                tx.Complete();
            }
            var message = _receiver.Peek("h");
            using (var tx = new TransactionScope())
            {
                _receiver.MoveTo("b", message);
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                message = _receiver.Receive("h", "b");
                Assert.Equal("subzero", Encoding.Unicode.GetString(message.Data));
                tx.Complete();
            }
        }

        [Fact]
        public void Moving_to_subqueue_should_remove_from_main_queue()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = Encoding.Unicode.GetBytes("subzero")
                    });
                tx.Complete();
            }
            var message = _receiver.Peek("h");
            using (var tx = new TransactionScope())
            {
                _receiver.MoveTo("b", message);
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                Assert.NotNull(_receiver.Receive("h", "b"));
                Assert.Throws<TimeoutException>(() => _receiver.Receive("h", TimeSpan.FromSeconds(1)));
                tx.Complete();
            }
        }

        [Fact]
        public void Moving_to_subqueue_will_not_be_completed_until_tx_is_completed()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = Encoding.Unicode.GetBytes("subzero")
                    });
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h");
                _receiver.MoveTo("b", message);
                Assert.Throws<TimeoutException>(() => _receiver.Receive("h", "b", TimeSpan.FromSeconds(1)));
                tx.Complete();
            }
        }

        [Fact]
        public void Moving_to_subqueue_will_be_reverted_by_transaction_rollback()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = Encoding.Unicode.GetBytes("subzero")
                    });
                tx.Complete();
            }
            using (new TransactionScope())
            {
                var message = _receiver.Receive("h");
                _receiver.MoveTo("b", message);
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h");
                Assert.NotNull(message);
                tx.Complete();
            }
        }

        [Fact]
        public void Can_scan_messages_in_main_queue_without_seeing_messages_from_subqueue()
        {
            using (var tx = new TransactionScope())
            {
                _receiver.EnqueueDirectlyTo("h", null, new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("1234")
                });
                _receiver.EnqueueDirectlyTo("h", "c", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("4321")
                });
                tx.Complete();
            }
            var messages = _receiver.GetAllMessages("h", null);
            Assert.Equal(1, messages.Length);
            Assert.Equal("1234", Encoding.Unicode.GetString(messages[0].Data));
            messages = _receiver.GetAllMessages("h", "c");
            Assert.Equal(1, messages.Length);
            Assert.Equal("4321", Encoding.Unicode.GetString(messages[0].Data));
        }

        [Fact]
        public void Can_get_list_of_subqueues()
        {
            using (var tx = new TransactionScope())
            {
                _receiver.EnqueueDirectlyTo("h", "b", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("1234")
                });
                _receiver.EnqueueDirectlyTo("h", "c", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("4321")
                });
                _receiver.EnqueueDirectlyTo("h", "c", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("4321")
                });
                _receiver.EnqueueDirectlyTo("h", "u", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("4321")
                });
                tx.Complete();
            }
            var q = _receiver.GetQueue("h");
            Assert.Equal(new[] { "b", "c", "u" }, q.GetSubqeueues());
        }

        [Fact]
        public void Can_get_number_of_messages()
        {
            using (var tx = new TransactionScope())
            {
                _receiver.EnqueueDirectlyTo("h", "b", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("1234")
                });
                _receiver.EnqueueDirectlyTo("h", "c", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("4321")
                });
                _receiver.EnqueueDirectlyTo("h", "c", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("4321")
                });
                _receiver.EnqueueDirectlyTo("h", "u", new MessagePayload
                {
                    Data = Encoding.Unicode.GetBytes("4321")
                });
                tx.Complete();
            }
            Assert.Equal(4, _receiver.GetNumberOfMessages("h"));
            Assert.Equal(4, _receiver.GetNumberOfMessages("h"));
        }

        public void Dispose()
        {
            _sender.Dispose();
            _receiver.Dispose();
        }
    }
}