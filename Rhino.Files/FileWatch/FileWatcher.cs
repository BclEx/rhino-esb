using Rhino.FileWatch.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rhino.FileWatch
{
    public abstract class FileWatcher
    {
        volatile TaskCompletionSource<FileWatcherFile> _tcs = new TaskCompletionSource<FileWatcherFile>();

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

        protected void WaitSetResult(FileWatcherFile result)
        {
            _tcs.SetResult(result);
            WaitReset();
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

        private void DoWaitAsync(AcceptAsyncResult asyncResult)
        {
            Task.Factory.StartNew(s => DoBeginAccept((AcceptAsyncResult)s), asyncResult, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
        }

        private void DoBeginAccept(AcceptAsyncResult asyncResult)
        {
            if (!Active)
                throw new InvalidOperationException("mustlisten");
            var success = FileWatcherError.Success;
            lock (this)
            {
                var result = _tcs.Task.Result;
                //WaitReset();
                success = result.Error;
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
}
