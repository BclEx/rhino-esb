using System;
using System.IO;
using System.Threading;

namespace Rhino.FileWatch
{
    public class FileWatcherFile
    {
        public string EndPoint { get; set; }

        public Stream GetStream()
        {
            return File.OpenRead(Path);
        }
        //new ManualResetEvent(false).WaitOne();

        public string Path { get; set; }
    }
}
