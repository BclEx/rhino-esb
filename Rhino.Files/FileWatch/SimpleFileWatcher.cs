using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rhino.FileWatch
{
    public class SimpleFileWatcher : FileWatcher
    {
        readonly FileSystemWatcher _watcher = new FileSystemWatcher();
        TaskCompletionSource<FileWatcherFile> _tcs = new TaskCompletionSource<FileWatcherFile>();

        public SimpleFileWatcher(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            _watcher.Path = directory + "\\";
            _watcher.Filter = "*.*";
            //_watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.NotifyFilter = NotifyFilters.LastWrite;
            _watcher.IncludeSubdirectories = true;
            _watcher.Changed += new FileSystemEventHandler(OnChanged);
            //_watcher.Created += new FileSystemEventHandler(OnChanged);
            //_watcher.Deleted += new FileSystemEventHandler(OnChanged);
            //_watcher.Renamed += new RenamedEventHandler(OnRenamed);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            _tcs.SetResult(new FileWatcherFile
            {
                EndPoint = "localhost",
                Path = e.FullPath,
            });
            WaitReset();
        }


        //private void OnRenamed(object sender, RenamedEventArgs e)
        //{
        //    _tcs.SetResult(new FileWatcherFile
        //    {
        //        EndPoint = "localhost",
        //        Path = e.FullPath,
        //    });
        //    Reset();
        //}

        public override FileWatcherFile WaitAsync(out FileWatcherError error)
        {
            _tcs.Task.Wait();
            error = FileWatcherError.Success;
            return _tcs.Task.Result;
        }

        private void WaitReset()
        {
            while (true)
            {
                var tcs = _tcs;
                if (!tcs.Task.IsCompleted || Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<FileWatcherFile>(), tcs) == tcs)
                    return;
            }
        }

        public override void Start() { base.Start(); _watcher.EnableRaisingEvents = Active; }
        public override void Stop() { base.Stop(); _watcher.EnableRaisingEvents = Active; }
    }
}

