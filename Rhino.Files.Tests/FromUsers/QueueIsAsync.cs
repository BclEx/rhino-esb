using Rhino.Files.Model;
using Rhino.Files.Protocol;
using Rhino.Files.Tests.Protocol;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests.FromUsers
{
    public class QueueIsAsync : WithDebugging, IDisposable
    {
        readonly QueueManager _queueManager;

        public QueueIsAsync()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            _queueManager = new QueueManager("localhost", "test.esent");
            _queueManager.CreateQueues("h");
            _queueManager.Start();
        }

        [Fact]
        public void CanReceiveFromQueue()
        {
            for (var i = 0; i < 2; i++)
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
							Data = Encoding.Unicode.GetBytes("hello-" + i),
							SentAt = DateTime.Now
						},
					}
                }.Send();
            }
            var longTx = new ManualResetEvent(false);
            var wait = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(state =>
            {
                using (var tx = new TransactionScope())
                {
                    _queueManager.Receive("h", null);
                    longTx.WaitOne();
                    tx.Complete();
                }
                wait.Set();
            });
            using (var tx = new TransactionScope())
            {
                var message = _queueManager.Receive("h", null);
                Assert.NotNull(message);
                tx.Complete();
            }
            longTx.Set();
            wait.WaitOne();
        }

        public void Dispose()
        {
            _queueManager.Dispose();
        }
    }
}