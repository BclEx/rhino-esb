using System;
using System.Threading;

namespace Rhino.FileWatch
{
    public class AcceptAsyncResult : IAsyncResult
    {
        object _event;
        int _intCompleted;
        object _result;
        bool _userEvent;

        public AcceptAsyncResult(object asyncObject, object state, AsyncCallback callback)
        {
            AsyncObject = asyncObject;
            AsyncState = state;
            AsyncCallback = callback;
        }

        public object AsyncObject { get; private set; }
        public object AsyncState { get; private set; }
        public AsyncCallback AsyncCallback { get; set; }
        public bool EndCalled { get; set; }
        public int ErrorCode { get; set; }

        private bool LazilyCreateEvent(out ManualResetEvent waitHandle)
        {
            waitHandle = new ManualResetEvent(false);
            try
            {
                if (Interlocked.CompareExchange(ref _event, waitHandle, null) == null)
                {
                    if (InternalPeekCompleted)
                        waitHandle.Set();
                    return true;
                }
                waitHandle.Close();
                waitHandle = (ManualResetEvent)_event;
                return false;
            }
            catch
            {
                _event = null;
                if (waitHandle != null)
                    waitHandle.Close();
                throw;
            }
        }

        protected virtual void Cleanup()
        {
        }

        #region Invoke

        [ThreadStatic]
        static ThreadContext _threadContext;

        private class ThreadContext
        {
            internal int NestedIOCount;
        }

        public void InvokeCallback(object result = null) { ProtectedInvokeCallback(result, IntPtr.Zero); }
        protected void ProtectedInvokeCallback(object result, IntPtr userToken)
        {
            if (result == DBNull.Value)
                throw new ArgumentNullException("result");
            if (((_intCompleted & 0x7fffffff) == 0) && ((Interlocked.Increment(ref _intCompleted) & 0x7fffffff) == 1))
            {
                if (_result == DBNull.Value)
                    _result = result;
                var event2 = (ManualResetEvent)_event;
                if (event2 != null)
                {
                    try { event2.Set(); }
                    catch (ObjectDisposedException) { }
                }
                Complete(userToken);
            }
        }

        protected virtual void Complete(IntPtr userToken)
        {
            var background = false;
            var currentThreadContext = CurrentThreadContext;
            try
            {
                currentThreadContext.NestedIOCount++;
                if (AsyncCallback != null)
                {
                    if (currentThreadContext.NestedIOCount >= 50)
                    {
                        ThreadPool.QueueUserWorkItem(new WaitCallback(WorkerThreadComplete));
                        background = true;
                    }
                    else
                        AsyncCallback(this);
                }
            }
            finally
            {
                currentThreadContext.NestedIOCount--;
                if (!background)
                    Cleanup();
            }
        }

        private void WorkerThreadComplete(object state)
        {
            try { AsyncCallback(this); }
            finally { Cleanup(); }
        }

        private static ThreadContext CurrentThreadContext
        {
            get
            {
                var context = _threadContext;
                if (context == null)
                {
                    context = new ThreadContext();
                    _threadContext = context;
                }
                return context;
            }
        }

        #endregion

        #region Wait

        public object WaitForCompletion(bool snap = true)
        {
            ManualResetEvent waitHandle = null;
            var flag = false;
            if (!(snap ? IsCompleted : InternalPeekCompleted))
            {
                waitHandle = (ManualResetEvent)_event;
                if (waitHandle == null)
                    flag = LazilyCreateEvent(out waitHandle);
            }
            if (waitHandle == null) goto end;
            try { waitHandle.WaitOne(-1, false); goto end; }
            catch (ObjectDisposedException) { goto end; }
            finally
            {
                if (flag && !_userEvent)
                {
                    var event2 = (ManualResetEvent)_event;
                    _event = null;
                    if (!_userEvent)
                        event2.Close();
                }
            }
        spin:
            Thread.SpinWait(1);
        end:
            if (_result == DBNull.Value)
                goto spin;
            return _result;
        }

        #endregion

        internal bool InternalPeekCompleted
        {
            get { return ((_intCompleted & 0x7fffffff) > 0); }
        }

        public bool IsCompleted
        {
            get
            {
                var intCompleted = _intCompleted;
                if (intCompleted == 0)
                    intCompleted = Interlocked.CompareExchange(ref _intCompleted, -2147483648, 0);
                return ((intCompleted & 0x7fffffff) > 0);
            }
        }

        public object Result
        {
            get { return (_result != DBNull.Value ? _result : null); }
            set { _result = value; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                _userEvent = true;
                if (_intCompleted == 0)
                    Interlocked.CompareExchange(ref _intCompleted, -2147483648, 0);
                var waitHandle = (ManualResetEvent)_event;
                while (waitHandle == null)
                    LazilyCreateEvent(out waitHandle);
                return waitHandle;
            }
        }

        public bool CompletedSynchronously
        {
            get
            {
                var intCompleted = _intCompleted;
                if (intCompleted == 0)
                    intCompleted = Interlocked.CompareExchange(ref _intCompleted, -2147483648, 0);
                return (intCompleted > 0);
            }
        }
    }
}
