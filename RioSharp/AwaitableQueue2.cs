using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public sealed class AwaitableQueue2 : INotifyCompletion //where T : class
    {
        RioBufferSegment _currentValue;
        Action _continuation = null;
        SpinLock s = new SpinLock();

        public bool IsCompleted
        {
            get
            {
                bool taken = false;
                s.Enter(ref taken);
                //if (!taken)
                //    throw new ArgumentException("fuu");
                var res = _currentValue != null;
                if (res)
                    s.Exit();
                return res;
            }
        }

        public void OnCompleted(Action continuation)
        {
            //bool taken = false;
            //s.Enter(ref taken);
            //if (!taken)
            //    throw new ArgumentException("fuu");
            //if (_continuation != null)
            //    throw new ArgumentException("fuu");

            //if (_currentValue != null)
            //    continuation();//throw new ArgumentException("fuu");
            //else
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

            var res = _continuation;
            _continuation = null;
            _currentValue = item;
            s.Exit();

            if (res != null)
                ThreadPool.QueueUserWorkItem(o => { res(); }, null);
        }

        public RioBufferSegment GetResult()
        {
            //bool taken = false;
            //s.Enter(ref taken);
            //if (!taken)
            //    throw new ArgumentException("fuu");
            var res = _currentValue;
            _currentValue = null;
            //s.Exit();
            return res;
        }

        public AwaitableQueue2 GetAwaiter() => this;

        public void Clear(Action<RioBufferSegment> cleanUp)
        {
            cleanUp(_currentValue);
            _currentValue = null;
            if (_continuation != null)
                _continuation();
        }
    }
}
