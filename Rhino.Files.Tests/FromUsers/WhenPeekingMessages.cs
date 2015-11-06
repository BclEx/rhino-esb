using System;
using System.IO;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests.FromUsers
{
    public class WhenPeekingMessages : IDisposable
    {
        readonly QueueManager _queueManager;

        public WhenPeekingMessages()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            _queueManager = new QueueManager("localhost", "test.esent");
            _queueManager.CreateQueues("h");
            _queueManager.Start();
        }

        [Fact]
        public void ItShouldNotDecrementMessageCount()
        {
            using (var tx = new TransactionScope())
            {
                _queueManager.EnqueueDirectlyTo("h", null, new MessagePayload
                {
                    Data = new byte[] { 1, 2, 4, 5 }
                });
                tx.Complete();
            }
            var count = _queueManager.GetNumberOfMessages("h");
            Assert.Equal(1, count);
            var msg = _queueManager.Peek("h");
            Assert.Equal(new byte[] { 1, 2, 4, 5 }, msg.Data);
            count = _queueManager.GetNumberOfMessages("h");
            Assert.Equal(1, count);
        }

        public void Dispose()
        {
            _queueManager.Dispose();
        }
    }
}