using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rhino.FileWatch
{
    public class SimpleFileWatcher : DebounceFileWatcher
    {
        readonly FileSystemWatcher _watcher = new FileSystemWatcher();

        public SimpleFileWatcher(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            _watcher.Path = directory + "\\";
            _watcher.Filter = "*.*";
            _watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            //_watcher.NotifyFilter = NotifyFilters.LastWrite;
            //_watcher.NotifyFilter = NotifyFilters.FileName;
            _watcher.IncludeSubdirectories = true;
            _watcher.Changed += new FileSystemEventHandler(OnChanged);
            _watcher.Created += new FileSystemEventHandler(OnChanged);
            _watcher.Deleted += new FileSystemEventHandler(OnChanged);
            _watcher.Renamed += new RenamedEventHandler(OnRenamed);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            //if (e.FullPath.EndsWith(".sending"))
            //    return;
            //WaitSetResult(new FileWatcherFile
            //{
            //    EndPoint = _watcher.Path,
            //    Paths = new[] { e.FullPath },
            //});
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (e.FullPath.EndsWith(".sending"))
                return;
            SetResult(new FileWatcherFile
            {
                EndPoint = _watcher.Path,
                Paths = new[] { e.FullPath },
                Error = FileWatcherError.Success,
            });
        }

        public override void Start() { base.Start(); _watcher.EnableRaisingEvents = Active; }
        public override void Stop() { base.Stop(); _watcher.EnableRaisingEvents = Active; }
    }
}

