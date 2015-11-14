using Rhino.FileWatch.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rhino.FileWatch
{
    public abstract class FileWatcher
    {
        volatile TaskCompletionSource<IEnumerable<FileWatcherFile>> _tcs = new TaskCompletionSource<IEnumerable<FileWatcherFile>>();

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

        #region WaitResult

        protected virtual void SetResult(IEnumerable<FileWatcherFile> result)
        {
            _tcs.SetResult(result);
            while (true)
            {
                var tcs = _tcs;
                if (!tcs.Task.IsCompleted || Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<IEnumerable<FileWatcherFile>>(), tcs) == tcs)
                    return;
            }
        }

        private void WaitResult(AcceptAsyncResult asyncResult)
        {
            Task.Factory.StartNew(s => WaitResultCallback((AcceptAsyncResult)s), asyncResult, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
        }

        private void WaitResultCallback(AcceptAsyncResult asyncResult)
        {
            if (!Active)
                throw new InvalidOperationException("mustlisten");
            foreach (var r in _tcs.Task.Result)
                switch (r.Error)
                {
                    case FileWatcherError.Success:
                        asyncResult.Result = r;
                        asyncResult.InvokeCallback();
                        break;
                    default:
                        //asyncResult.ErrorCode = (int)r.Error;
                        throw new FileWatcherException(r.Error);
                }
            //var success = FileWatcherError.Success;
            //IEnumerable<FileWatcherFile> result;
            //lock (this)
            //{
            //result = _tcs.Task.Result;
            //success = result.Error;
            //switch (success)
            //{
            //    case FileWatcherError.Success:
            //        asyncResult.Result = result;
            //        break;
            //    default:
            //        asyncResult.ErrorCode = (int)success;
            //        break;
            //}
            //}

        }

        #endregion

        #region AcceptFile

        public IAsyncResult BeginAcceptFile(AsyncCallback callback, object state)
        {
            if (!Active)
                throw new InvalidOperationException("watch_stopped");
            // async
            var asyncResult = new AcceptAsyncResult(this, state, callback);
            WaitResult(asyncResult);
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
}
