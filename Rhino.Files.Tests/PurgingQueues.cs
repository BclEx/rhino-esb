using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests
{
    public class PurgingQueues
    {
        private const string EsentFileName = "test.esent";
        private QueueManager _queueManager;

        public PurgingQueues()
        {
            if (Directory.Exists(EsentFileName))
                Directory.Delete(EsentFileName, true);
        }

        [Fact(Skip = "This is a slow load test")]
        public void CanPurgeLargeSetsOfOldData()
        {
            _queueManager = new QueueManager("localhost", EsentFileName);
            _queueManager.Configuration.OldestMessageInOutgoingHistory = TimeSpan.Zero;
            _queueManager.Start();

            // Seed the queue with historical messages to be purged
            QueueMessagesThreaded(1000);
            //Parallel.For(0, 1000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => SendMessages());

            _queueManager.WaitForAllMessagesToBeSent();

            // Try to purge while still sending new messages.
            var waitHandle = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                _queueManager.PurgeOldData();
                Console.WriteLine("Finished purging data");
                waitHandle.Set();
            });
            QueueMessagesThreaded(10000);
            //var purgeTask = Task.Factory.StartNew(() =>
            //{
            //    queueManager.PurgeOldData();
            //    Console.WriteLine("Finished purging data");
            //});
            //Parallel.For(0, 10000, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => SendMessages());

            waitHandle.WaitOne();
            //purgeTask.Wait();
            _queueManager.WaitForAllMessagesToBeSent();

            _queueManager.PurgeOldData();

            Assert.Equal(_queueManager.Configuration.NumberOfMessagesToKeepInOutgoingHistory, _queueManager.GetAllSentMessages().Length);
        }

        private void QueueMessagesThreaded(int iterations)
        {
            const int threadCount = 8;
            int iterationsPerThread = iterations / threadCount;
            var threads = new List<Thread>();
            for (int i = 0; i < threadCount; i++)
            {
                var thread = new Thread(() =>
                {
                    for (int j = 0; j < iterationsPerThread; j++)
                        SendMessages();
                });
                thread.Start();
                threads.Add(thread);
            }
            threads.ForEach(x => x.Join());
        }

        private void SendMessages()
        {
            using (var scope = new TransactionScope())
            {
                for (int j = 0; j < 100; j++)
                    _queueManager.Send(new Uri("file://" + _queueManager.Endpoint), new MessagePayload { Data = new byte[0] });
                scope.Complete();
            }
        }
    }
}