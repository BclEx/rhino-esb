using Rhino.Files.Model;
using Rhino.Files.Protocol;
using Rhino.Files.Tests.Protocol;
using System;
using System.IO;
using System.Threading;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests
{
    public class RaisingSendEvents : WithDebugging
    {
        MessageEventArgs _messageEventArgs;

        public QueueManager SetupSender()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            if (Directory.Exists("test2.esent"))
                Directory.Delete("test2.esent", true);
            var sender = new QueueManager(null, "test.esent");
            sender.Start();
            _messageEventArgs = null;
            return sender;
        }

        void RecordMessageEvent(object s, MessageEventArgs e)
        {
            _messageEventArgs = e;
        }

        [Fact]
        public void MessageQueuedForSend_EventIsRaised()
        {
            using (var sender = SetupSender())
            {
                sender.MessageQueuedForSend += RecordMessageEvent;

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

                sender.MessageQueuedForSend -= RecordMessageEvent;
            }

            Assert.NotNull(_messageEventArgs);
            Assert.Equal("localhost", _messageEventArgs.Endpoint);
            Assert.Equal("h", _messageEventArgs.Message.Queue);
        }

        [Fact]
        public void MessageQueuedForSend_EventIsRaised_EvenIfTransactionFails()
        {
            using (var sender = SetupSender())
            {
                sender.MessageQueuedForSend += RecordMessageEvent;

                using (new TransactionScope())
                {
                    sender.Send(
                        new Uri("file://_error/h"),
                        new MessagePayload
                        {
                            Data = new byte[] { 1, 2, 4, 5 }
                        });
                }

                sender.MessageQueuedForSend -= RecordMessageEvent;
            }

            Assert.NotNull(_messageEventArgs);
            Assert.Equal("_error", _messageEventArgs.Endpoint);
            Assert.Equal("h", _messageEventArgs.Message.Queue);
        }

        [Fact]
        public void MessageSent_EventIsRaised()
        {
            using (var sender = SetupSender())
            {
                sender.MessageSent += RecordMessageEvent;

                using (var receiver = new QueueManager("localhost", "test2.esent"))
                {
                    receiver.CreateQueues("h");
                    receiver.Start();

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
                }

                sender.MessageSent -= RecordMessageEvent;
            }

            Assert.NotNull(_messageEventArgs);
            Assert.Equal("localhost", _messageEventArgs.Endpoint);
            Assert.Equal("h", _messageEventArgs.Message.Queue);
        }

        [Fact]
        public void MessageSent_EventNotRaised_IfNotSent()
        {
            using (var sender = SetupSender())
            {
                sender.MessageSent += RecordMessageEvent;

                using (var tx = new TransactionScope())
                {
                    sender.Send(
                        new Uri("file://_error/h"),
                        new MessagePayload
                        {
                            Data = new byte[] { 1, 2, 4, 5 }
                        });
                    tx.Complete();
                }
                Thread.Sleep(1000);

                sender.MessageSent -= RecordMessageEvent;
            }

            Assert.Null(_messageEventArgs);
        }

        [Fact]
        public void MessageSent_EventNotRaised_IfReceiverReverts()
        {
            using (var sender = SetupSender())
            {
                sender.MessageSent += RecordMessageEvent;

                using (var receiver = new RevertingQueueManager("localhost", "test2.esent"))
                {
                    receiver.CreateQueues("h");
                    receiver.Start();

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

                    sender.MessageSent -= RecordMessageEvent;
                }
            }

            Assert.Null(_messageEventArgs);
        }

        private class RevertingQueueManager : QueueManager
        {
            public RevertingQueueManager(string endpoint, string path)
                : base(endpoint, path) { }

            protected override IMessageAcceptance AcceptMessages(Message[] msgs)
            {
                throw new Exception("Cannot accept messages.");
            }
        }
    }
}