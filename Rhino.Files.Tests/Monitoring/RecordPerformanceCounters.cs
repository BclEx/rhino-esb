using Rhino.Files.Model;
using Rhino.Files.Monitoring;
using Rhino.Mocks;
using System;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests.Monitoring
{
    public class RecordPerformanceCounters
    {
        TestablePerformanceMonitor _performanceMonitor;
        IQueueManager _queueManager;

        private void Setup()
        {
            _queueManager = MockRepository.GenerateStub<IQueueManager>();
            _queueManager.Stub(qm => qm.Queues).Return(new string[0]);
            _queueManager.Stub(qm => qm.GetMessagesCurrentlySending()).Return(new PersistentMessageToSend[0]);
            _queueManager.Stub(qm => qm.Endpoint).Return("localhost");

            _performanceMonitor = new TestablePerformanceMonitor(_queueManager);
        }

        [Fact]
        public void MessageQueuedForSend_should_updatet_correct_instance()
        {
            TestEventUpdatesCorrectInstance(
                qm => qm.MessageQueuedForSend += null,
                new Message { Queue = "q" },
                "localhost/q");
        }

        [Fact]
        public void MessageSent_should_updatet_correct_instance()
        {
            TestEventUpdatesCorrectInstance(
                qm => qm.MessageSent += null,
                new Message { Queue = "q" },
                "localhost/q");
        }

        [Fact]
        public void MessageQueuedForReceive_should_updatet_correct_instance()
        {
            TestEventUpdatesCorrectInstance(
                qm => qm.MessageQueuedForReceive += null,
                new Message { Queue = "q" },
                "127.0.0.1/q");
        }

        [Fact]
        public void MessageReceived_should_updatet_correct_instance()
        {
            TestEventUpdatesCorrectInstance(
                qm => qm.MessageReceived += null,
                new Message { Queue = "q" },
                "127.0.0.1/q");
        }

        private void TestEventUpdatesCorrectInstance(Action<IQueueManager> @event, Message message, string expectedInstanceName)
        {
            Setup();
            var e = new MessageEventArgs("localhost", message);
            _queueManager.Raise(@event, null, e);
            Assert.Equal(expectedInstanceName, _performanceMonitor.InstanceName);
        }


        [Fact]
        public void MessageQueuedForSend_without_transaction_should_increment_UnsentMessages()
        {
            Setup();
            _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages = 0;
            var e = new MessageEventArgs("localhost", new Message { Queue = "q" });
            _queueManager.Raise(qm => qm.MessageQueuedForSend += null, null, e);
            Assert.Equal(1, _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages);
        }

        [Fact]
        public void MessageQueuedForSend_in_committed_transaction_should_increment_UnsentMessages()
        {
            Setup();
            _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages = 0;
            using (var tx = new TransactionScope())
            {
                var e = new MessageEventArgs("localhost", new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageQueuedForSend += null, null, e);
                tx.Complete();
            }
            Assert.Equal(1, _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages);
        }

        [Fact]
        public void MessageQueuedForSend_in_transaction_should_not_increment_UnsentMessages_prior_to_commit()
        {
            Setup();
            _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages = 0;
            using (var tx = new TransactionScope())
            {
                var e = new MessageEventArgs("localhost", new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageQueuedForSend += null, null, e);
                Assert.Equal(0, _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages);
                tx.Complete();
            }
        }

        [Fact]
        public void MessageQueuedForSend_in_failed_transaction_should_not_increment_UnsentMessages()
        {
            Setup();
            _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages = 0;
            using (new TransactionScope())
            {
                var e = new MessageEventArgs("localhost", new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageQueuedForSend += null, null, e);
            }
            Assert.Equal(0, _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages);
        }

        [Fact]
        public void MessageSent_without_transaction_should_decrement_UnsentMessages()
        {
            Setup();
            _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages = 1;
            var e = new MessageEventArgs("localhost", new Message { Queue = "q" });
            _queueManager.Raise(qm => qm.MessageSent += null, null, e);
            Assert.Equal(0, _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages);
        }

        [Fact]
        public void MessageSent_in_committed_transaction_should_decrement_UnsentMessages()
        {
            Setup();
            _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages = 1;
            using (var tx = new TransactionScope())
            {
                var e = new MessageEventArgs("localhost", new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageSent += null, null, e);
                tx.Complete();
            }
            Assert.Equal(0, _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages);
        }

        [Fact]
        public void MessageSent_in_transaction_should_not_decrement_UnsentMessages_prior_to_commit()
        {
            Setup();
            _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages = 1;
            using (var tx = new TransactionScope())
            {
                var e = new MessageEventArgs("localhost", new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageSent += null, null, e);
                Assert.Equal(1, _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages);
                tx.Complete();
            }
        }

        [Fact]
        public void MessageSent_in_failed_transaction_should_not_decrement_UnsentMessages()
        {
            Setup();
            _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages = 1;
            using (new TransactionScope())
            {
                var e = new MessageEventArgs("localhost", new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageSent += null, null, e);
            }
            Assert.Equal(1, _performanceMonitor.OutboundPerfomanceCounters.UnsentMessages);
        }

        [Fact]
        public void MessageQueuedForReceive_without_transaction_should_increment_ArrivedMessages()
        {
            Setup();
            _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages = 0;
            var e = new MessageEventArgs(null, new Message { Queue = "q" });
            _queueManager.Raise(qm => qm.MessageQueuedForReceive += null, null, e);
            Assert.Equal(1, _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages);
        }

        [Fact]
        public void MessageQueuedForReceive_in_committed_transaction_should_increment_ArrivedMessages()
        {
            Setup();
            _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages = 0;
            using (var tx = new TransactionScope())
            {
                var e = new MessageEventArgs(null, new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageQueuedForReceive += null, null, e);
                tx.Complete();
            }
            Assert.Equal(1, _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages);
        }

        [Fact]
        public void MessageQueuedForReceive_in_transaction_not_should_increment_ArrivedMessages_prior_to_commit()
        {
            Setup();
            _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages = 0;
            using (var tx = new TransactionScope())
            {
                var e = new MessageEventArgs(null, new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageQueuedForReceive += null, null, e);
                Assert.Equal(0, _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages);
                tx.Complete();
            }
        }

        [Fact]
        public void MessageQueuedForReceive_in_failed_transaction_not_should_increment_ArrivedMessages()
        {
            Setup();
            _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages = 0;
            using (new TransactionScope())
            {
                var e = new MessageEventArgs(null, new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageQueuedForReceive += null, null, e);
            }
            Assert.Equal(0, _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages);
        }

        [Fact]
        public void MessageReceived_without_transaction_should_decrement_ArrivedMessages()
        {
            Setup();
            _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages = 1;
            var e = new MessageEventArgs(null, new Message { Queue = "q" });
            _queueManager.Raise(qm => qm.MessageReceived += null, null, e);
            Assert.Equal(0, _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages);
        }

        [Fact]
        public void MessageReceived_in_committed_transaction_should_decrement_ArrivedMessages()
        {
            Setup();
            _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages = 1;
            using (var tx = new TransactionScope())
            {
                var e = new MessageEventArgs(null, new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageReceived += null, null, e);
                tx.Complete();
            }
            Assert.Equal(0, _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages);
        }

        [Fact]
        public void MessageReceived_in_transaction_should_not_decrement_ArrivedMessages_prior_to_commit()
        {
            Setup();
            _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages = 1;
            using (var tx = new TransactionScope())
            {
                var e = new MessageEventArgs(null, new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageReceived += null, null, e);
                Assert.Equal(1, _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages);
                tx.Complete();
            }
        }

        [Fact]
        public void MessageReceived_in_failed_transaction_should_not_decrement_ArrivedMessages()
        {
            Setup();
            _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages = 1;
            using (new TransactionScope())
            {
                var e = new MessageEventArgs(null, new Message { Queue = "q" });
                _queueManager.Raise(qm => qm.MessageReceived += null, null, e);
            }
            Assert.Equal(1, _performanceMonitor.InboundPerfomanceCounters.ArrivedMessages);
        }

        private class TestablePerformanceMonitor : PerformanceMonitor
        {
            readonly TestProvider _testProvider = new TestProvider();

            public TestablePerformanceMonitor(IQueueManager queueManager)
                : base(queueManager) { }

            public string InstanceName { get { return _testProvider.InstanceName; } }
            public IOutboundPerfomanceCounters OutboundPerfomanceCounters { get { return _testProvider.OutboundPerfomanceCounters; } }
            public IInboundPerfomanceCounters InboundPerfomanceCounters { get { return _testProvider.InboundPerfomanceCounters; } }

            protected override void AssertCountersExist() { } // Do nothing as we don't need real counters to exist for our purposes here.

            protected override IPerformanceCountersProvider ImmediatelyRecordingProvider
            {
                get { return _testProvider; }
            }

            private class TestProvider : IPerformanceCountersProvider
            {
                public string InstanceName;
                public readonly IInboundPerfomanceCounters InboundPerfomanceCounters = MockRepository.GenerateStub<IInboundPerfomanceCounters>();
                public readonly IOutboundPerfomanceCounters OutboundPerfomanceCounters = MockRepository.GenerateStub<IOutboundPerfomanceCounters>();

                public IOutboundPerfomanceCounters GetOutboundCounters(string instanceName)
                {
                    InstanceName = instanceName;
                    return OutboundPerfomanceCounters;
                }

                public IInboundPerfomanceCounters GetInboundCounters(string instanceName)
                {
                    InstanceName = instanceName;
                    return InboundPerfomanceCounters;
                }
            }
        }
    }

}
