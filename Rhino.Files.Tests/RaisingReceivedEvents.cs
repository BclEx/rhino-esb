using Rhino.Files.Model;
using Rhino.Files.Tests.Protocol;
using System;
using System.IO;
using System.Threading;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests
{
    public class RaisingReceivedEvents : WithDebugging, IDisposable
    {
        MessageEventArgs _messageEventArgs;
        MessageEventArgs _messageEventArgs2;
        int _messageEventCount;
        int _messageEventCount2;
        QueueManager _lastCreatedSender;
        QueueManager _lastCreatedReceiver;

        public QueueManager SetupSender()
        {
            // Needed because tests that are terminated by XUnit due to a timeout are terminated rudely such that using statements do not dispose of their objects.
            if (_lastCreatedSender != null)
                _lastCreatedSender.Dispose();
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            _lastCreatedSender = new QueueManager(null, "test.esent");
            _lastCreatedSender.Start();
            return _lastCreatedSender;
        }

        public QueueManager SetupReciever()
        {
            // Needed because tests that are terminated by XUnit due to a timeout are terminated rudely such that using statements do not dispose of their objects.
            if (_lastCreatedReceiver != null)
                _lastCreatedReceiver.Dispose();
            if (Directory.Exists("test2.esent"))
                Directory.Delete("test2.esent", true);
            _lastCreatedReceiver = new QueueManager("localhost", "test2.esent");
            _lastCreatedReceiver.CreateQueues("h", "b");
            _lastCreatedReceiver.Start();
            ResetEventRecorder();
            return _lastCreatedReceiver;
        }

        private void ResetEventRecorder()
        {
            _messageEventArgs = null;
            _messageEventArgs2 = null;
            _messageEventCount = 0;
            _messageEventCount2 = 0;
        }

        void RecordMessageEvent(object s, MessageEventArgs e)
        {
            _messageEventArgs = e;
            _messageEventCount++;
        }

        void RecordMessageEvent2(object s, MessageEventArgs e)
        {
            _messageEventArgs2 = e;
            _messageEventCount2++;
        }

        [Fact(Timeout = 15000)]
        public void MessageQueuedForReceive_EventIsRaised()
        {
            using (var sender = SetupSender())
            using (var receiver = SetupReciever())
            {
                receiver.MessageQueuedForReceive += RecordMessageEvent;

                using (var tx = new TransactionScope())
                {
                    sender.Send(
                        new Uri("file://localhost/h"),
                        new MessagePayload
                        {
                            Data = new byte[] { 1, 2, 4, 5 }
                        });

                    tx.Complete();
                }

                while (_messageEventCount == 0)
                    Thread.Sleep(100);

                receiver.MessageQueuedForReceive -= RecordMessageEvent;
            }

            Assert.NotNull(_messageEventArgs);
            Assert.Equal("h", _messageEventArgs.Message.Queue);
        }

        [Fact(Timeout = 15000)]
        public void MessageQueuedForReceive_EventIsRaised_DirectEnqueuing()
        {
            using (var receiver = SetupReciever())
            {
                receiver.MessageQueuedForReceive += RecordMessageEvent;

                using (var tx = new TransactionScope())
                {
                    receiver.EnqueueDirectlyTo("h", null, new MessagePayload { Data = new byte[] { 1, 2, 3 } });

                    tx.Complete();
                }
                while (_messageEventCount == 0)
                    Thread.Sleep(100);

                receiver.MessageQueuedForReceive -= RecordMessageEvent;
            }

            Assert.NotNull(_messageEventArgs);
            Assert.Equal("h", _messageEventArgs.Message.Queue);
        }

        [Fact]
        public void MessageQueuedForReceive_EventNotRaised_IfReceiveAborts()
        {
            var wait = new ManualResetEvent(false);

            using (var sender = new FakeSender
            {
                Destination = "localhost",
                FailToAcknowledgeReceipt = true,
                Messages = new[] { new Message
                {
                    Id = new MessageId { MessageIdentifier = Guid.NewGuid(), SourceInstanceId = Guid.NewGuid() },
                    SentAt = DateTime.Now,
                    Queue = "h", 
                    Data = new byte[] { 1, 2, 4, 5 }
                } }
            })
            {
                sender.SendCompleted += () => wait.Set();
                using (var receiver = SetupReciever())
                {
                    receiver.MessageQueuedForReceive += RecordMessageEvent;

                    sender.Send();
                    wait.WaitOne();

                    Thread.Sleep(1000);

                    receiver.MessageQueuedForReceive -= RecordMessageEvent;
                }
            }

            Assert.Null(_messageEventArgs);
        }

        [Fact]
        public void MessageReceived_EventIsRaised()
        {
            using (var sender = SetupSender())
            using (var receiver = SetupReciever())
            {
                receiver.MessageReceived += RecordMessageEvent;

                using (var tx = new TransactionScope())
                {
                    sender.Send(
                        new Uri("file://localhost/h"),
                        new MessagePayload
                        {
                            Data = new byte[] { 1, 2, 4, 5 }
                        });

                    tx.Complete();
                }
                sender.WaitForAllMessagesToBeSent();

                using (var tx = new TransactionScope())
                {
                    receiver.Receive("h");
                    tx.Complete();
                }

                receiver.MessageReceived -= RecordMessageEvent;
            }

            Assert.NotNull(_messageEventArgs);
            Assert.Equal("h", _messageEventArgs.Message.Queue);
        }

        [Fact]
        public void MessageReceived_EventNotRaised_IfMessageNotReceived()
        {
            using (var sender = SetupSender())
            using (var receiver = SetupReciever())
            {
                receiver.MessageReceived += RecordMessageEvent;

                using (var tx = new TransactionScope())
                {
                    sender.Send(
                        new Uri("file://localhost/h"),
                        new MessagePayload
                        {
                            Data = new byte[] { 1, 2, 4, 5 }
                        });

                    tx.Complete();
                }
                Thread.Sleep(1000);

                receiver.MessageReceived -= RecordMessageEvent;
            }

            Assert.Null(_messageEventArgs);
        }

        [Fact(Timeout = 15000)]
        public void MessageReceived_and_MessageQueuedForReceive_events_raised_when_message_removed_and_moved()
        {
            using (var sender = SetupSender())
            using (var receiver = SetupReciever())
            {
                receiver.MessageReceived += RecordMessageEvent;
                receiver.MessageQueuedForReceive += RecordMessageEvent2;

                using (var tx = new TransactionScope())
                {
                    sender.Send(
                        new Uri("file://localhost/h"),
                        new MessagePayload
                        {
                            Data = new byte[] { 1, 2, 4, 5 }
                        });

                    tx.Complete();
                }

                while (_messageEventCount2 == 0)
                    Thread.Sleep(100);

                ResetEventRecorder();

                using (var tx = new TransactionScope())
                {
                    var message = receiver.Receive("h");
                    receiver.MoveTo("b", message);
                    tx.Complete();
                }

                receiver.MessageReceived -= RecordMessageEvent;
                receiver.MessageQueuedForReceive -= RecordMessageEvent;

                Assert.Equal(1, _messageEventCount);
                Assert.NotNull(_messageEventArgs);
                Assert.Equal("h", _messageEventArgs.Message.Queue);
                Assert.Null(_messageEventArgs.Message.SubQueue);

                Assert.Equal(1, _messageEventCount2);
                Assert.NotNull(_messageEventArgs2);
                Assert.Equal("h", _messageEventArgs2.Message.Queue);
                Assert.Equal("b", _messageEventArgs2.Message.SubQueue);
            }
        }

        [Fact(Timeout = 15000)]
        public void MessageReceived_and_MessageQueuedForReceive_events_raised_when_message_peeked_and_moved()
        {
            using (var sender = SetupSender())
            using (var receiver = SetupReciever())
            {
                receiver.MessageReceived += RecordMessageEvent;
                receiver.MessageQueuedForReceive += RecordMessageEvent2;

                using (var tx = new TransactionScope())
                {
                    sender.Send(
                        new Uri("file://localhost/h"),
                        new MessagePayload
                        {
                            Data = new byte[] { 1, 2, 4, 5 }
                        });

                    tx.Complete();
                }

                while (_messageEventCount2 == 0)
                    Thread.Sleep(100);

                ResetEventRecorder();

                using (var tx = new TransactionScope())
                {
                    var message = receiver.Peek("h");
                    receiver.MoveTo("b", message);
                    tx.Complete();
                }

                receiver.MessageReceived -= RecordMessageEvent;
                receiver.MessageQueuedForReceive -= RecordMessageEvent2;

                Assert.Equal(1, _messageEventCount);
                Assert.NotNull(_messageEventArgs);
                Assert.Equal("h", _messageEventArgs.Message.Queue);
                Assert.Null(_messageEventArgs.Message.SubQueue);

                Assert.Equal(1, _messageEventCount2);
                Assert.NotNull(_messageEventArgs2);
                Assert.Equal("h", _messageEventArgs2.Message.Queue);
                Assert.Equal("b", _messageEventArgs2.Message.SubQueue);
            }
        }

        public void Dispose()
        {
            if (_lastCreatedSender != null)
                _lastCreatedSender.Dispose();
            if (_lastCreatedReceiver != null)
                _lastCreatedReceiver.Dispose();
        }
    }
}