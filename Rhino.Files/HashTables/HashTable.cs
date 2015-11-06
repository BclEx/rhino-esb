using System;
using System.IO;
using System.Threading;

namespace Rhino.HashTables
{
    public class HashTable : IDisposable
    {
        readonly string _database;
        readonly ReaderWriterLockSlim _lockObject = new ReaderWriterLockSlim();
        readonly string _path;

        public HashTable(string database)
        {
            _database = database;
            _path = database;
            if (!Path.IsPathRooted(database))
                _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, database);
            _database = Path.Combine(_path, Path.GetFileName(database));
        }

        [CLSCompliant(false)]
        public void Batch(Action<HashTableActions> action)
        {
            var primaryLock = !_lockObject.IsReadLockHeld;
            try
            {
                if (primaryLock)
                    _lockObject.EnterReadLock();
                for (int i = 0; i < 5; i++)
                    try
                    {
                        using (var actions = new HashTableActions(_database))
                            action(actions);
                        return;
                    }
                    catch (HashTableException e)
                    {
                        if (e.Error != HashTableError.WriteConflict)
                            throw;
                        Thread.Sleep(10);
                    }
            }
            finally { if (primaryLock) _lockObject.ExitReadLock(); }
        }

        public void Dispose()
        {
            _lockObject.EnterWriteLock();
            try
            {
                GC.SuppressFinalize(this);
            }
            finally { _lockObject.ExitWriteLock(); }
        }

        public void Initialize()
        {
            try
            {
            }
            catch (Exception e)
            {
                Dispose();
                throw new InvalidOperationException("Could not open cache: " + _database, e);
            }
        }
    }
}
