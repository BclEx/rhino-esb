using Common.Logging;
using System;
using System.IO;
using System.Threading;

namespace Rhino.Files.Storage
{
    public class QueueStorage : IDisposable
    {
        readonly ILog _log = LogManager.GetLogger(typeof(QueueStorage));
        readonly QueueManagerConfiguration _configuration;
        readonly string _database;
        readonly Guid _instanceId;
        readonly ReaderWriterLockSlim _usageLock = new ReaderWriterLockSlim();

        public QueueStorage(string database, QueueManagerConfiguration configuration)
        {
            _configuration = configuration;
            _database = (!Path.IsPathRooted(database) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, database) : database);
            _instanceId = new Guid();
        }

        #region Dispose

        public void Initialize()
        {
            try { if (!Directory.Exists(_database)) Directory.CreateDirectory(_database); }
            catch (Exception e) { Dispose(); throw new InvalidOperationException("Could not open queue: " + _database, e); }
        }

        public void Dispose()
        {
            _usageLock.EnterWriteLock();
            try
            {
                _log.Debug("Disposing queue storage");
                try
                {
                    GC.SuppressFinalize(this);
                }
                catch (Exception e) { _log.Error("Could not dispose of queue storage properly", e); throw; }
            }
            finally { _usageLock.ExitWriteLock(); }
        }

        public void DisposeRudely()
        {
            _usageLock.EnterWriteLock();
            try
            {
                _log.Debug("Rudely disposing queue storage");
                try
                {
                    GC.SuppressFinalize(this);
                }
                catch (Exception e) { _log.Error("Could not dispose of queue storage properly", e); throw; }
            }
            finally { _usageLock.ExitWriteLock(); }
        }

        #endregion

        public void Global(Action<GlobalActions> action)
        {
            var primaryLock = !_usageLock.IsReadLockHeld;
            try
            {
                if (primaryLock)
                    _usageLock.EnterReadLock();
                using (var actions = new GlobalActions(_database, _instanceId, _configuration))
                    action(actions);
            }
            finally { if (primaryLock) _usageLock.ExitReadLock(); }
        }

        public void Send(Action<SenderActions> action)
        {
            var primaryLock = !_usageLock.IsReadLockHeld;
            try
            {
                if (primaryLock)
                    _usageLock.EnterReadLock();
                using (var actions = new SenderActions(_database, _instanceId, _configuration))
                    action(actions);
            }
            finally { if (primaryLock) _usageLock.ExitReadLock(); }
        }

        public Guid Id { get; private set; }
    }
}

