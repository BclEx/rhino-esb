using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace Rhino.FileWatch
{
    public class DebounceFileWatcher : FileWatcher
    {
        readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        readonly Dictionary<Tuple<string, string>, int> _signals = new Dictionary<Tuple<string, string>, int>();
        Timer _timer;
        readonly int _period;
        readonly int _debounce;

        public DebounceFileWatcher(int period = 500, int debounce = 5)
        {
            _period = period;
            _debounce = debounce;
        }

        private void TimerCallback(object state)
        {
            var paths = new List<Tuple<string, string>>();
            _lock.EnterWriteLock();
            foreach (var key in _signals.Keys.ToArray())
                if (_signals[key]++ > _debounce)
                {
                    paths.Add(key);
                    _signals.Remove(key);
                }
            _lock.ExitWriteLock();
            if (paths.Count > 0)
                base.SetResult(
                    paths.GroupBy(x => x.Item1, x => x.Item2)
                        .Select(x => new FileWatcherFile
                        {
                            EndPoint = x.Key,
                            Paths = x.ToArray(),
                            Error = FileWatcherError.Success,
                        }));
        }

        protected virtual void SetResult(FileWatcherFile result)
        {
            if (result.Error != FileWatcherError.Success)
            {
                base.SetResult(new[] { result });
                return;
            }
            _lock.EnterWriteLock();
            var endPoint = result.EndPoint;
            foreach (var path in result.Paths)
                _signals[new Tuple<string, string>(endPoint, path)] = 0;
            _lock.ExitWriteLock();
        }

        public override void Start() { base.Start(); _timer = new Timer(TimerCallback, null, 0, _period); }
        public override void Stop() { base.Stop(); if (_timer != null) { _timer.Dispose(); _timer = null; } }
    }
}

