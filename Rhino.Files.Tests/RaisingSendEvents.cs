using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Transactions;
using Rhino.Files.Model;
using Rhino.Files.Protocol;
using Rhino.Files.Tests.Protocol;
using Xunit;

namespace Rhino.Files.Tests
{
    public class RaisingSendEvents : WithDebugging
    {
        private const string TEST_QUEUE_1 = "testA.esent";
        private const string TEST_QUEUE_2 = "testB.esent";

        private MessageEventArgs messageEventArgs;

        public QueueManager SetupSender()
        {
            if (Directory.Exists(TEST_QUEUE_1))
                Directory.Delete(TEST_QUEUE_1, true);

            if (Directory.Exists(TEST_QUEUE_2))
                Directory.Delete(TEST_QUEUE_2, true);

            var sender = new QueueManager("localhost", TEST_QUEUE_1);
            sender.Start();
            messageEventArgs = null;
            return sender;
        }

        void RecordMessageEvent(object s, MessageEventArgs e)
        {
            messageEventArgs = e;
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

            Assert.NotNull(messageEventArgs);
            Assert.Equal("localhost", messageEventArgs.Endpoint);
            Assert.Equal("h", messageEventArgs.Message.Queue);
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
                        new Uri("file://localhost/h"),
                        new MessagePayload
                        {
                            Data = new byte[] { 1, 2, 4, 5 }
                        });

                }

                sender.MessageQueuedForSend -= RecordMessageEvent;
            }

            Assert.NotNull(messageEventArgs);
            Assert.Equal("localhost", messageEventArgs.Endpoint);
            Assert.Equal("h", messageEventArgs.Message.Queue);
        }

        [Fact]
        public void MessageSent_EventIsRaised()
        {
            using (var sender = SetupSender())
            {
                sender.MessageSent += RecordMessageEvent;

                using (var receiver = new QueueManager("localhost", TEST_QUEUE_2))
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

            Assert.NotNull(messageEventArgs);
            Assert.Equal("localhost", messageEventArgs.Endpoint);
            Assert.Equal("h", messageEventArgs.Message.Queue);
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

            Assert.Null(messageEventArgs);
        }

        [Fact]
        public void MessageSent_EventNotRaised_IfReceiverReverts()
        {
            using (var sender = SetupSender())
            {
                sender.MessageSent += RecordMessageEvent;

                using (var receiver = new RevertingQueueManager("localhost", TEST_QUEUE_2))
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

            Assert.Null(messageEventArgs);
        }

        private class RevertingQueueManager : QueueManager
        {
            public RevertingQueueManager(string endpoint, string path)
                : base(endpoint, path)
            {
            }

            protected override IMessageAcceptance AcceptMessages(Message[] msgs)
            {
                throw new Exception("Cannot accept messages.");
            }
        }
    }
}