using Rhino.Files.Tests.Protocol;
using System;
using System.IO;
using Xunit;

namespace Rhino.Files.Tests
{
    public class StartingRhinoQueues : WithDebugging, IDisposable
    {
        readonly QueueManager _queueManager;

        public StartingRhinoQueues()
        {
            if (Directory.Exists("test.esent"))
                Directory.Delete("test.esent", true);
            _queueManager = new QueueManager("localhost", "test.esent");
        }

        [Fact]
        public void Starting_twice_should_throw()
        {
            _queueManager.Start();
            Assert.Throws<InvalidOperationException>(() => _queueManager.Start());
        }

        [Fact]
        public void Starting_after_dispose_should_throw()
        {
            _queueManager.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _queueManager.Start());
        }

        public void Dispose()
        {
            _queueManager.Dispose();
        }
    }
}
