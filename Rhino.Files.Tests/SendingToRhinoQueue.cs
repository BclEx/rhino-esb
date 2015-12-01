using Rhino.Files.Tests.Protocol;
using System;
using System.IO;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests
{
    public class SendingToRhinoQueue : WithDebugging, IDisposable
    {
        readonly QueueManager _sender, _receiver;

        public SendingToRhinoQueue()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            if (Directory.Exists("test2.esent"))
                Directory.Delete("test2.esent", true);
            _sender = new QueueManager(null, "test.esent");
            _sender.Start();
            _receiver = new QueueManager("localhost", "test2.esent");
            _receiver.CreateQueues("h", "a");
            _receiver.Start();
        }

        [Fact]
        public void CanSendToQueue()
        {
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
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", null);
                Assert.Equal(new byte[] { 1, 2, 4, 5 }, message.Data);
                tx.Complete();
            }
        }

        [Fact]
        public void SendingTwoMessages_OneOfWhichToUnknownQueue_WillStillWork()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 1, 2, 4, 5 }
                    });
                _sender.Send(
                    new Uri("file://localhost/I_dont_exists"),
                    new MessagePayload
                    {
                        Data = new byte[] { 1, 2, 4, 5 }
                    });
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", null, TimeSpan.FromSeconds(5));
                Assert.Equal(new byte[] { 1, 2, 4, 5 }, message.Data);
                tx.Complete();
            }
        }

        [Fact]
        public void CanSendHeaders()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 1, 2, 4, 5 },
                        Headers =
                        {
                            {"id","6"},
                            {"date","2009-01-10"}
                        }
                    });
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", null);
                Assert.Equal("6", message.Headers["id"]);
                Assert.Equal("2009-01-10", message.Headers["date"]);
                tx.Complete();
            }
        }

        [Fact]
        public void CanLookAtSentMessages()
        {
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
            using (var tx = new TransactionScope())
            {
                _receiver.Receive("h", null);
                tx.Complete();
            }
            var messages = _sender.GetAllSentMessages();
            Assert.Equal(1, messages.Length);
            Assert.Equal(new byte[] { 1, 2, 4, 5 }, messages[0].Data);
        }

        [Fact]
        public void CanLookAtMessagesCurrentlySending()
        {
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
        public void WillNotSendIfTxIsNotCommitted()
        {
            using (new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 1, 2, 4, 5 }
                    });
            }
            using (var tx = new TransactionScope())
            {
                Assert.Throws<TimeoutException>(() => _receiver.Receive("h", "subqueue", TimeSpan.FromSeconds(1)));
                tx.Complete();
            }
        }

        [Fact]
        public void CanSendSeveralMessagesToQueue()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 1, 2, 4, 5 }
                    });
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 4, 5, 6, 7 }
                    });
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 6, 7, 8, 9 }
                    });
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", null);
                Assert.Equal(new byte[] { 1, 2, 4, 5 }, message.Data);
                message = _receiver.Receive("h", null);
                Assert.Equal(new byte[] { 4, 5, 6, 7 }, message.Data);
                message = _receiver.Receive("h", null);
                Assert.Equal(new byte[] { 6, 7, 8, 9 }, message.Data);
                tx.Complete();
            }
        }

        [Fact]
        public void CanSendMessagesToSeveralQueues()
        {
            using (var tx = new TransactionScope())
            {
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 1, 2, 4, 5 }
                    });
                _sender.Send(
                    new Uri("file://localhost/a"),
                    new MessagePayload
                    {
                        Data = new byte[] { 4, 5, 6, 7 }
                    });
                _sender.Send(
                    new Uri("file://localhost/h"),
                    new MessagePayload
                    {
                        Data = new byte[] { 6, 7, 8, 9 }
                    });
                tx.Complete();
            }
            using (var tx = new TransactionScope())
            {
                var message = _receiver.Receive("h", null);
                Assert.Equal(new byte[] { 1, 2, 4, 5 }, message.Data);
                message = _receiver.Receive("h", null);
                Assert.Equal(new byte[] { 6, 7, 8, 9 }, message.Data);
                message = _receiver.Receive("a", null);
                Assert.Equal(new byte[] { 4, 5, 6, 7 }, message.Data);
                tx.Complete();
            }
        }

        public void Dispose()
        {
            _sender.Dispose();
            _receiver.Dispose();
        }
    }
}