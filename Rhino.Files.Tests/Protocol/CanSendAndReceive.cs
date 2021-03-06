using Rhino.Files.Model;
using Rhino.Files.Protocol;
using Rhino.Mocks;
using System;
using System.Threading;
using Xunit;

namespace Rhino.Files.Tests.Protocol
{
    public class CanSendAndReceive : WithDebugging
    {
        [Fact]
        public void OneMessage()
        {
            var wait = new ManualResetEvent(false);
            Message[] recievedMsgs = null;
            var endPoint = "localhost";
            using (var reciever = new Receiver("localhost", messages =>
            {
                recievedMsgs = messages;
                return MockRepository.GenerateStub<IMessageAcceptance>();
            }))
            {
                reciever.CompletedRecievingMessages += () => wait.Set();
                reciever.Start();
                new Sender
                {
                    Destination = endPoint,
                    Messages = new[]
                    {
                        new Message
                        {
                            Data = new byte[] {1, 2, 4, 5, 6},
                            SentAt = new DateTime(2001, 1, 1),
                            Queue = "hello doggy",
                            Id = MessageId.GenerateRandom()
                        },
                    }
                }.Send();
                wait.WaitOne();
                Assert.Equal(1, recievedMsgs.Length);
                Assert.Equal("hello doggy", recievedMsgs[0].Queue);
                Assert.Equal(new byte[] { 1, 2, 4, 5, 6 }, recievedMsgs[0].Data);
                Assert.Equal(new DateTime(2001, 1, 1), recievedMsgs[0].SentAt);
            }
        }

        [Fact]
        public void TwoMessagesInSeparateCalls()
        {
            var wait = new ManualResetEvent(false);
            Message[] recievedMsgs = null;
            using (var reciever = new Receiver("localhost", messages =>
            {
                recievedMsgs = messages;
                return MockRepository.GenerateStub<IMessageAcceptance>();
            }))
            {
                reciever.CompletedRecievingMessages += () => wait.Set();
                reciever.Start();
                new Sender
                {
                    Destination = "localhost",
                    Messages = new[]
                    {
                        new Message
                        {
                            Data = new byte[] {1, 2, 4, 5, 6},
                            SentAt = new DateTime(2001, 1, 1),
                            Queue = "hello doggy",
                            Id = MessageId.GenerateRandom()
                        },
                    }
                }.Send();
                wait.WaitOne();
                Assert.Equal(1, recievedMsgs.Length);
                wait.Reset();
                new Sender
                {
                    Destination = "localhost",
                    Messages = new[]
                    {
                        new Message
                        {
                            Data = new byte[] {1, 2, 4, 5, 6},
                            SentAt = new DateTime(2001, 1, 1),
                            Queue = "hello doggy2",
                            Id = MessageId.GenerateRandom()
                        },
                    }
                }.Send();
                wait.WaitOne();
                Assert.Equal(1, recievedMsgs.Length);
                Assert.Equal("hello doggy2", recievedMsgs[0].Queue);
            }
        }

        [Fact]
        public void TwoMessagesInOneCall()
        {
            var wait = new ManualResetEvent(false);
            Message[] recievedMsgs = null;
            using (var reciever = new Receiver("localhost", messages =>
            {
                recievedMsgs = messages;
                return MockRepository.GenerateStub<IMessageAcceptance>();
            }))
            {
                reciever.CompletedRecievingMessages += () => wait.Set();
                reciever.Start();
                new Sender
                {
                    Destination = "localhost",
                    Messages = new[]
                    {
                        new Message
                        {
                            Data = new byte[] {1, 2, 4, 5, 6},
                            SentAt = new DateTime(2001, 1, 1),
                            Queue = "hello doggy",
                            Id = MessageId.GenerateRandom()
                        },
                         new Message
                        {
                            Data = new byte[] {1, 2, 4, 5, 6},
                            SentAt = new DateTime(2001, 1, 1),
                            Queue = "hello doggy2",
                            Id = MessageId.GenerateRandom()
                        },
                    }
                }.Send();
                wait.WaitOne();
                Assert.Equal(2, recievedMsgs.Length);
                Assert.Equal("hello doggy", recievedMsgs[0].Queue);
                Assert.Equal("hello doggy2", recievedMsgs[1].Queue);
            }
        }
    }
}