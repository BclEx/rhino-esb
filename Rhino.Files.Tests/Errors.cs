using System;
using System.IO;
using System.Threading;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests
{
    public class Errors : IDisposable
    {
        readonly QueueManager _sender;

        public Errors()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);

            _sender = new QueueManager("localhost", "test.esent");
            _sender.Start();
        }
        [Fact]
        public void Will_get_notified_when_failed_to_send_to_endpoint()
        {
            var wait = new ManualResetEvent(false);
            string endPointWeFailedToSendTo = null;
            _sender.FailedToSendMessagesTo += endpoint =>
            {
                endPointWeFailedToSendTo = endpoint;
                wait.Set();
            };
            using (var tx = new TransactionScope())
            {
                _sender.Send(new Uri("file://255.255.255.255/hello/world"), new MessagePayload
                {
                    Data = new byte[] { 1 }
                });
                tx.Complete();
            }
            wait.WaitOne();
            Assert.Equal("255.255.255.255", endPointWeFailedToSendTo);
        }

        [Fact]
        public void Will_not_exceed_sending_thresholds()
        {
            var wait = new ManualResetEvent(false);
            int maxNumberOfConnecting = 0;
            _sender.FailedToSendMessagesTo += endpoint =>
            {
                maxNumberOfConnecting = Math.Max(maxNumberOfConnecting, _sender.CurrentlyConnectingCount);
                if (endpoint.Equals("foo50"))
                    wait.Set();
            };
            using (var tx = new TransactionScope())
            {
                for (int i = 0; i < 200; ++i)
                    _sender.Send(new Uri(string.Format("file://foo{0}/hello/world", i)), new MessagePayload
                    {
                        Data = new byte[] { 1 }
                    });
                tx.Complete();
            }
            wait.WaitOne();
            Assert.True(maxNumberOfConnecting < 32);
        }

        public void Dispose()
        {
            _sender.Dispose();
        }
    }
}