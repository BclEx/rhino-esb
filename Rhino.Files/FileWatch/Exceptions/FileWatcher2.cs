using Common.Logging;
using Rhino.FileWatch.Exceptions;
using System;
using System.Threading;
using Queue = System.Collections.Queue;

namespace Rhino.FileWatch
{
#if false
    public abstract class FileWatcher : IDisposable
    {
        protected readonly ILog _logger = LogManager.GetLogger(typeof(FileWatcher));

        protected bool Active { get; set; }
        protected bool CleanedUp { get; set; }

        public void Dispose() { Dispose(true); } //GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                int num;
                while ((num = Interlocked.CompareExchange(ref _intCleanedUp, 1, 0)) == 2)
                    Thread.SpinWait(1);
                if (num == 1)
                    return;
                else
                {
                    var queue = _acceptQueueResult;
                    if (queue != null && queue.Count != 0)
                        ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(CompleteAcceptResults), null);
                    if (_asyncEvent != null)
                        _asyncEvent.Close();
                }
            }
        }

        public void Close()
        {
            if (_logger != null) _logger.DebugFormat("Enter:Close");
            Dispose();
            if (_logger != null) _logger.DebugFormat("Exit:Close");
        }

        public virtual void Start()
        {
            if (_logger != null) _logger.DebugFormat("Enter:Start");
            if (Active)
            {
                if (_logger != null) _logger.DebugFormat("Exit:Start");
                return;
            }
            try
            {
                if (CleanedUp)
                    throw new ObjectDisposedException(base.GetType().FullName);
            }
            catch (Exception) { Stop(); throw; }
            Active = true;
            if (_logger != null) _logger.DebugFormat("Exit:Start");
        }

        public virtual void Stop()
        {
            if (_logger != null) _logger.DebugFormat("Enter:Stop");
            Dispose(true);
            Active = false;
            if (_logger != null) _logger.DebugFormat("Exit:Stop");
        }

    #region WaitAndQueue

        Queue _acceptQueueResult;

        public abstract FileWatcherError DoWaitResult(out object result);

        public void DoBeginAccept(AcceptAsyncResult asyncResult)
        {
            if (!Active)
                throw new InvalidOperationException("mustlisten");
            var processed = false;
            var watcher = this;
            var success = FileWatcherError.Success;
            var acceptQueue = GetAcceptQueue();
            var lockTaken = false;
            try
            {
                Monitor.Enter(watcher, ref lockTaken);
                if (acceptQueue.Count == 0)
                {
                    object result = null;
                    try { success = DoWaitResult(out result); }
                    catch (ObjectDisposedException) { success = FileWatcherError.OperationAborted; }
                    switch (success)
                    {
                        case FileWatcherError.WouldBlock:
                            acceptQueue.Enqueue(asyncResult);
                            if (!SetAsyncEventSelect())
                            {
                                acceptQueue.Dequeue();
                                throw new ObjectDisposedException(base.GetType().FullName);
                            }
                            goto end;
                        case FileWatcherError.Success:
                            asyncResult.Result = result;
                            break;
                        default:
                            asyncResult.ErrorCode = (int)success;
                            break;
                    }
                    processed = true;
                }
                else
                    acceptQueue.Enqueue(asyncResult);
            }
            finally { if (lockTaken) Monitor.Exit(watcher); }
        end:
            if (!processed)
                return;
            if (success == FileWatcherError.Success)
                asyncResult.InvokeCallback();
            else
            {
                var ex = new FileWatcherException(success);
                if (_logger != null) _logger.Debug("Exception:DoBeginAccept", ex);
                throw ex;
            }
        }

        private Queue GetAcceptQueue()
        {
            if (_acceptQueueResult == null)
                Interlocked.CompareExchange(ref _acceptQueueResult, new Queue(0x10), null);
            return _acceptQueueResult;
        }

        private void AcceptCallback(object nullState)
        {
            var acceptQueue = GetAcceptQueue();
            var more = true;
            while (more)
            {
                AcceptAsyncResult result = null;
                var success = FileWatcherError.OperationAborted;
                var lockTaken = false;
                var watcher = this;
                try
                {
                    Monitor.Enter(watcher, ref lockTaken);
                    if (acceptQueue.Count == 0)
                        break;
                    result = (AcceptAsyncResult)acceptQueue.Peek();
                    object result2 = null;
                    if (!CleanedUp)
                    {
                        try { success = DoWaitResult(out result2); }
                        catch (ObjectDisposedException) { success = FileWatcherError.OperationAborted; }
                    }
                    else if (success == FileWatcherError.Success)
                        result.Result = result;
                    else
                        result.ErrorCode = (int)success;
                    acceptQueue.Dequeue();
                    if (acceptQueue.Count == 0)
                    {
                        if (!CleanedUp)
                            UnsetAsyncEventSelect();
                        more = false;
                    }
                }
                finally { if (lockTaken) Monitor.Exit(watcher); }
                try { result.InvokeCallback(result); continue; }
                catch { if (more) ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(AcceptCallback), nullState); throw; }
            }
        }

        #endregion

    #region AsyncEventSelect

        int _intCleanedUp;
        RegisteredWaitHandle _registeredWait;
        ManualResetEvent _asyncEvent;
        static volatile WaitOrTimerCallback _registeredWaitCallback;

        private void CompleteAcceptResults(object nullState)
        {
            var acceptQueue = GetAcceptQueue();
            var more = true;
            while (more)
            {
                AcceptAsyncResult result = null;
                var lockTaken = false;
                var watcher = this;
                try
                {
                    Monitor.Enter(watcher, ref lockTaken);
                    if (acceptQueue.Count == 0)
                        break;
                    result = (AcceptAsyncResult)acceptQueue.Dequeue();
                    if (acceptQueue.Count == 0)
                        more = false;
                }
                finally { if (lockTaken) Monitor.Exit(watcher); }
                try { result.InvokeCallback(new FileWatcherException(FileWatcherError.OperationAborted)); continue; }
                catch { if (more) ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(CompleteAcceptResults), null); throw; }
            }
        }

        private bool SetAsyncEventSelect()
        {
            if (_registeredWait != null)
                return false;
            if (_asyncEvent == null)
            {
                Interlocked.CompareExchange(ref _asyncEvent, new ManualResetEvent(false), null);
                if (_registeredWaitCallback == null)
                    _registeredWaitCallback = new WaitOrTimerCallback(RegisteredWaitCallback);
            }
            if (Interlocked.CompareExchange(ref _intCleanedUp, 2, 0) != 0)
                return false;
            try { _registeredWait = ThreadPool.UnsafeRegisterWaitForSingleObject(_asyncEvent, _registeredWaitCallback, this, -1, true); }
            finally { Interlocked.Exchange(ref _intCleanedUp, 0); }
            return true;
        }

        private void UnsetAsyncEventSelect()
        {
            var registeredWait = _registeredWait;
            if (registeredWait != null)
            {
                _registeredWait = null;
                registeredWait.Unregister(null);
            }
            if (_asyncEvent != null)
            {
                try { _asyncEvent.Reset(); }
                catch (ObjectDisposedException) { }
            }
        }

        private static void RegisteredWaitCallback(object state, bool timedOut)
        {
            var watcher = (FileWatcher)state;
            if (Interlocked.Exchange<RegisteredWaitHandle>(ref watcher._registeredWait, null) != null)
                watcher.AcceptCallback(null);
        }


        #endregion

    #region AcceptFile

        public IAsyncResult BeginAcceptFile(AsyncCallback callback, object state)
        {
            if (!Active)
                throw new InvalidOperationException("watch_stopped");
            // async
            if (_logger != null) _logger.DebugFormat("Enter:BeginAcceptFile");
            if (CleanedUp)
                throw new ObjectDisposedException(base.GetType().FullName);
            var asyncResult = new AcceptAsyncResult(this, state, callback);
            asyncResult.StartAsync();
            DoBeginAccept(asyncResult);
            asyncResult.FinishAsync();
            if (_logger != null) _logger.InfoFormat("Exit:BeginAcceptFile {0}", asyncResult);
            return asyncResult;
        }

        public FileWatcherFile EndAcceptFile(IAsyncResult result)
        {
            if (!Active)
                throw new InvalidOperationException("watch_stopped");
            // async
            if (_logger != null) _logger.DebugFormat("Enter:EndAcceptFile {0}", result);
            if (CleanedUp)
                throw new ObjectDisposedException(base.GetType().FullName);
            if (result == null)
                throw new ArgumentNullException("result");
            var result2 = (result as AcceptAsyncResult);
            if (result2 == null || result2.AsyncObject != this)
                throw new ArgumentException("InvalidAsyncResult", "result");
            if (result2.EndCalled)
                throw new InvalidOperationException("InvalidEndCall[EndAcceptFile]");
            var retObject = result2.WaitForCompletion();
            result2.EndCalled = true;
            var e = (retObject as Exception);
            if (e != null)
                throw e;
            var file = (FileWatcherFile)retObject;
            if (_logger != null)
            {
                _logger.InfoFormat("file_accepted {0}", file.EndPoint);
                _logger.DebugFormat("Exit:EndAcceptFile {0}", retObject);
            }
            return file;
        }

        #endregion
    }
#endif
}