using Rhino.Files.Model;
using Rhino.Files.Protocol;
using Rhino.Files.Tests.Protocol;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests
{
    public class ReceivingFromRhinoQueue : WithDebugging, IDisposable
    {
        readonly QueueManager _queueManager;

        public ReceivingFromRhinoQueue()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            _queueManager = new QueueManager("localhost", "test.esent");
            _queueManager.CreateQueues("h");
            _queueManager.Start();
        }

        #region IDisposable Members

        public void Dispose()
        {
            _queueManager.Dispose();
        }

        #endregion

        [Fact]
        public void CanReceiveFromQueue()
        {
            new Sender
            {
                Destination = "localhost",
                Failure = exception => Assert.False(true),
                Success = () => null,
                Messages = new[]
                {
                    new Message
                    {
                        Id = MessageId.GenerateRandom(),
                        Queue = "h",
                        Data = Encoding.Unicode.GetBytes("hello"),
                        SentAt = DateTime.Now
                    },
                }
            }.Send();

            using (var tx = new TransactionScope())
            {
                var message = _queueManager.Receive("h", null);
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
                tx.Complete();
            }

            using (var tx = new TransactionScope())
            {
                Assert.Throws<TimeoutException>(() => _queueManager.Receive("h", null, TimeSpan.Zero));
                tx.Complete();
            }
        }

        [Fact]
        public void WhenSendingDuplicateMessageTwiceWillGetItOnlyOnce()
        {
            var msg = new Message
            {
                Id = MessageId.GenerateRandom(),
                Queue = "h",
                Data = Encoding.Unicode.GetBytes("hello"),
                SentAt = DateTime.Now
            };
            for (var i = 0; i < 2; i++)
            {
                var wait = new ManualResetEvent(false);
                var sender = new Sender
                {
                    Destination = "localhost",
                    Failure = exception => Assert.False(true),
                    Success = () => null,
                    Messages = new[] { msg },
                };
                sender.SendCompleted += () => wait.Set();
                sender.Send();
                wait.WaitOne();
            }

            using (var tx = new TransactionScope())
            {
                var message = _queueManager.Receive("h", null);
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
                tx.Complete();
            }

            using (var tx = new TransactionScope())
            {
                Assert.Throws<TimeoutException>(() => _queueManager.Receive("h", null, TimeSpan.Zero));
                tx.Complete();
            }
        }

        [Fact]
        public void WhenRevertingTransactionMessageGoesBackToQueue()
        {
            new Sender
            {
                Destination = "localhost",
                Failure = exception => Assert.False(true),
                Success = () => null,
                Messages = new[]
                {
                    new Message
                    {
                        Id = MessageId.GenerateRandom(),
                        Queue = "h",
                        Data = Encoding.Unicode.GetBytes("hello"),
                        SentAt = DateTime.Now
                    },
                }
            }.Send();

            using (new TransactionScope())
            {
                var message = _queueManager.Receive("h", null);
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
            }

            using (new TransactionScope())
            {
                var message = _queueManager.Receive("h", null);
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
            }
        }

        [Fact]
        public void CanLookupProcessedMessages()
        {
            new Sender
            {
                Destination = "localhost",
                Failure = exception => Assert.False(true),
                Success = () => null,
                Messages = new[]
                {
                    new Message
                    {
                        Id = MessageId.GenerateRandom(),
                        Queue = "h",
                        Data = Encoding.Unicode.GetBytes("hello"),
                        SentAt = DateTime.Now
                    },
                }
            }.Send();

            using (var tx = new TransactionScope())
            {
                var message = _queueManager.Receive("h", null);
                Assert.Equal("hello", Encoding.Unicode.GetString(message.Data));
                tx.Complete();
            }
            Thread.Sleep(500);

            var messages = _queueManager.GetAllProcessedMessages("h");
            Assert.Equal(1, messages.Length);
            Assert.Equal("hello", Encoding.Unicode.GetString(messages[0].Data));
        }

        [Fact]
        public void CanPeekExistingMessages()
        {
            new Sender
            {
                Destination = "localhost",
                Failure = exception => Assert.False(true),
                Success = () => null,
                Messages = new[]
                {
                    new Message
                    {
                        Id = MessageId.GenerateRandom(),
                        Queue = "h",
                        Data = Encoding.Unicode.GetBytes("hello"),
                        SentAt = DateTime.Now
                    },
                }
            }.Send();

            using (new TransactionScope())
            {
                // force a wait until we receive the message
                _queueManager.Receive("h", null);
            }

            var messages = _queueManager.GetAllMessages("h", null);
            Assert.Equal(1, messages.Length);
            Assert.Equal("hello", Encoding.Unicode.GetString(messages[0].Data));
        }
    }
}
