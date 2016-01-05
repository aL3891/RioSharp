using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RioSharp
{
    public sealed class AwaitableQueue2 : INotifyCompletion, IDisposable //where T : class
    {
        RioBufferSegment _currentValue;
        Action _continuation = null;
        WaitCallback continuationWrapperDelegate;
        SpinLock s = new SpinLock();

        public AwaitableQueue2()
        {
            continuationWrapperDelegate = continuationWrapper;
        }

        private void continuationWrapper(object o)
        {
            var res = _continuation;
            _continuation = null;
            res();
        }

        public bool IsCompleted
        {
            get
            {
                bool taken = false;
                s.Enter(ref taken);
                var res = _currentValue != null;
                if (res)
                    s.Exit();
                return res;
            }
        }

        public void OnCompleted(Action continuation)
        {
           _continuation = continuation;
            s.Exit();
        }

        public void Set(RioBufferSegment item)
        {
            bool taken = false;
            s.Enter(ref taken);
            //if (!taken)
            //    throw new ArgumentException("fuu");

            //if (_currentValue != null)
            //    throw new ArgumentException("fuu");
            
            _currentValue = item;
            s.Exit();

            if (_continuation != null)
                ThreadPool.QueueUserWorkItem(continuationWrapperDelegate, null);
        }

        public RioBufferSegment GetResult()
        {
            var res = _currentValue;
            _currentValue = null;
            return res;
        }

        public AwaitableQueue2 GetAwaiter() => this;

        public void Dispose()
        {
            _currentValue?.Dispose();
            _currentValue = null;
            if (_continuation != null)
                _continuation();
        }
    }
}
