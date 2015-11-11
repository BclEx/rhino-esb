
using Common.Logging;
using Rhino.FileWatch.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Queue = System.Collections.Queue;

namespace Rhino.FileWatch
{
#if true
    public abstract class FileWatcher
    {
        protected bool Active { get; set; }

        public virtual void Start()
        {
            if (Active)
                return;
            Active = true;
        }

        public virtual void Stop()
        {
            Active = false;
        }

        #region WaitAndQueue

        public abstract FileWatcherFile WaitAsync(out FileWatcherError error);

        private void DoWaitAsync(AcceptAsyncResult asyncResult)
        {
            Task.Factory.StartNew(s => DoBeginAccept((AcceptAsyncResult)s), asyncResult, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
        }

        private void DoBeginAccept(AcceptAsyncResult asyncResult)
        {
            if (!Active)
                throw new InvalidOperationException("mustlisten");
            var watcher = this;
            var success = FileWatcherError.Success;
            var lockTaken = false;
            try
            {
                Monitor.Enter(watcher, ref lockTaken);
                object result = null;
                result = WaitAsync(out success);
                switch (success)
                {
                    case FileWatcherError.Success:
                        asyncResult.Result = result;
                        break;
                    default:
                        asyncResult.ErrorCode = (int)success;
                        break;
                }
            }
            finally { if (lockTaken) Monitor.Exit(watcher); }
            if (success == FileWatcherError.Success)
                asyncResult.InvokeCallback();
            else
                throw new FileWatcherException(success);
        }

        #endregion

        #region AcceptFile

        public IAsyncResult BeginAcceptFile(AsyncCallback callback, object state)
        {
            if (!Active)
                throw new InvalidOperationException("watch_stopped");
            // async
            var asyncResult = new AcceptAsyncResult(this, state, callback);
            DoWaitAsync(asyncResult);
            return asyncResult;
        }

        public FileWatcherFile EndAcceptFile(IAsyncResult result)
        {
            if (!Active)
                throw new InvalidOperationException("watch_stopped");
            // async
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
            return (FileWatcherFile)retObject;
        }

        #endregion
    }
#endif
}
