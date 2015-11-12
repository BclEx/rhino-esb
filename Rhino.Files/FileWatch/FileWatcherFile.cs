using System;
using System.IO;
using System.Threading;

namespace Rhino.FileWatch
{
    public class FileWatcherFile
    {
        public string EndPoint { get; set; }
        public string[] Paths { get; set; }
        public FileWatcherError Error { get; set; }
    }
}
