using System;
using System.Collections.Generic;
using System.Threading;

namespace Rhino.Files.Utils
{
    internal class ThreadSafeSet<T>
    {
        readonly HashSet<T> _inner = new HashSet<T>();
        readonly ReaderWriterLockSlim _rwl = new ReaderWriterLockSlim();

        public void Add(IEnumerable<T> items)
        {
            _rwl.EnterWriteLock();
            try { foreach (var item in items) _inner.Add(item); }
            finally { _rwl.ExitWriteLock(); }
        }

        public IEnumerable<TK> Filter<TK>(IEnumerable<TK> items, Func<TK, T> translator)
        {
            _rwl.EnterReadLock();
            try
            {
                foreach (var item in items)
                {
                    if (_inner.Contains(translator(item)))
                        continue;
                    yield return item;
                }
            }
            finally { _rwl.ExitReadLock(); }
        }

        public void Remove(IEnumerable<T> items)
        {
            _rwl.EnterWriteLock();
            try { foreach (var item in items) _inner.Remove(item); }
            finally { _rwl.ExitWriteLock(); }
        }
    }
}

