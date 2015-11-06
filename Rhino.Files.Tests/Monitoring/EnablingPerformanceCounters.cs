﻿using Rhino.Files.Monitoring;
using Rhino.Files.Tests.Protocol;
using System;
using System.Diagnostics;
using System.IO;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests.Monitoring
{
    public class EnablingPerformanceCounters : WithDebugging
    {
        const string TEST_QUEUE_1 = "testA.esent";

        public void Setup()
        {
            if (Directory.Exists(TEST_QUEUE_1))
                Directory.Delete(TEST_QUEUE_1, true);
            PerformanceCategoryCreator.CreateCategories();
        }

        [AdminOnlyFact]
        public void Enabling_performance_counters_without_existing_categories_throws_meaningful_error()
        {
            PerformanceCounterCategoryCreation.DeletePerformanceCounters();
            using (var queueManager = new QueueManager("localhost", TEST_QUEUE_1))
                Assert.Throws<ApplicationException>(() => queueManager.EnablePerformanceCounters());
        }

        [AdminOnlyFact]
        public void Enabling_performance_counters_after_queue_has_started_should_throw()
        {
            Setup();
            using (var queueManager = new QueueManager("localhost", TEST_QUEUE_1))
            {
                queueManager.Start();
                Assert.Throws<InvalidOperationException>(() => queueManager.EnablePerformanceCounters());
            }
        }

        [AdminOnlyFact]
        public void Enabling_performance_counters_should_syncronize_counters_with_current_queue_state()
        {
            Setup();
            using (var queueManager = new QueueManager("localhost", TEST_QUEUE_1))
            {
                EnqueueMessages(queueManager);
                queueManager.EnablePerformanceCounters();
            }
            AssertAllCountersHaveCorrectValues();
        }

        [AdminOnlyFact]
        public void After_enabling_performance_counters_changes_to_queue_state_should_be_reflected_in_counters()
        {
            Setup();
            using (var queueManager = new QueueManager("localhost", TEST_QUEUE_1))
            {
                queueManager.EnablePerformanceCounters();
                EnqueueMessages(queueManager);
            }
            AssertAllCountersHaveCorrectValues();
        }

        private void EnqueueMessages(QueueManager queueManager)
        {
            queueManager.CreateQueues("Z");
            using (var tx = new TransactionScope())
            {
                queueManager.EnqueueDirectlyTo("Z", "", new MessagePayload { Data = new byte[] { 1, 2, 3 } });
                queueManager.EnqueueDirectlyTo("Z", "y", new MessagePayload { Data = new byte[] { 1, 2, 3 } });
                queueManager.Send(new Uri("file://localhost/A"), new MessagePayload { Data = new byte[] { 1, 2, 3 } });
                queueManager.Send(new Uri("file://localhost/A/b"), new MessagePayload { Data = new byte[] { 1, 2, 3 } });
                tx.Complete();
            }
        }

        private void AssertAllCountersHaveCorrectValues()
        {
            var unsentQueueCounter = new PerformanceCounter(OutboundPerfomanceCounters.CATEGORY, OutboundPerfomanceCounters.UNSENT_COUNTER_NAME, "localhost/A");
            var unsentSubQueueCounter = new PerformanceCounter(OutboundPerfomanceCounters.CATEGORY, OutboundPerfomanceCounters.UNSENT_COUNTER_NAME, "localhost/A/b");
            var arrivedQueueCounter = new PerformanceCounter(InboundPerfomanceCounters.CATEGORY, InboundPerfomanceCounters.ARRIVED_COUNTER_NAME, "127.0.0.1/Z");
            var arrivedSubQueueCounter = new PerformanceCounter(InboundPerfomanceCounters.CATEGORY, InboundPerfomanceCounters.ARRIVED_COUNTER_NAME, "127.0.0.1/Z/y");
            Assert.Equal(1, unsentQueueCounter.RawValue);
            Assert.Equal(1, unsentSubQueueCounter.RawValue);
            Assert.Equal(1, arrivedQueueCounter.RawValue);
            Assert.Equal(1, arrivedSubQueueCounter.RawValue);
        }
    }
}
