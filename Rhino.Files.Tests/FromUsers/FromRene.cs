using Rhino.Files.Model;
using Rhino.Files.Tests.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Transactions;
using Xunit;

namespace Rhino.Files.Tests.FromUsers
{
    public class FromRene : WithDebugging, IDisposable
    {
        readonly QueueManager _receiver;
        volatile bool _keepRunning = true;
        readonly List<string> _msgs = new List<string>();

        public FromRene()
        {
            if (Directory.Exists("receiver.esent"))
                Directory.Delete("receiver.esent", true);
            if (Directory.Exists("sender.esent"))
                Directory.Delete("sender.esent", true);
            using (var tx = new TransactionScope())
            {
                _receiver = new QueueManager("localhost", "receiver.esent");
                _receiver.CreateQueues("uno");
                _receiver.Start();
                tx.Complete();
            }
        }

        public void Sender(int count)
        {
            using (var sender = new QueueManager("localhost", "sender.esent"))
            {
                sender.Start();
                using (var tx = new TransactionScope())
                {
                    sender.Send(new Uri("file://localhost/uno"), new MessagePayload
                    {
                        Data = Encoding.ASCII.GetBytes("Message " + count)
                    });
                    tx.Complete();
                }
                sender.WaitForAllMessagesToBeSent();
            }
        }

        public void Receiver(object ignored)
        {
            while (_keepRunning)
                using (var tx = new TransactionScope())
                {
                    Message msg;
                    try { msg = _receiver.Receive("uno", null, new TimeSpan(0, 0, 10)); }
                    catch (TimeoutException) { continue; }
                    catch (ObjectDisposedException) { continue; }
                    lock (_msgs)
                    {
                        _msgs.Add(Encoding.ASCII.GetString(msg.Data));
                        Console.WriteLine(_msgs.Count);
                    }
                    tx.Complete();
                }
        }

        [Fact]
        public void ShouldOnlyGetTwoItems()
        {
            ThreadPool.QueueUserWorkItem(Receiver);
            Sender(4);
            Sender(5);
            while (true)
            {
                lock (_msgs)
                    if (_msgs.Count > 1)
                        break;
                Thread.Sleep(100);
            }
            Thread.Sleep(2000); // let it try to do something in addition to that
            _receiver.Dispose();
            _keepRunning = false;
            Assert.Equal(2, _msgs.Count);
            Assert.Equal("Message 4", _msgs[0]);
            Assert.Equal("Message 5", _msgs[1]);
        }

        public void Dispose()
        {
            _receiver.Dispose();
        }
    }
}
