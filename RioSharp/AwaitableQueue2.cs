using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public class AwaitableQueue2<T> : INotifyCompletion where T : class
    {
        ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        T _currentValue, nextValue;
        Action _continuation = null;

        public bool IsCompleted => _currentValue != nextValue;

        public void OnCompleted(Action continuation)
        {
            if (Interlocked.Exchange(ref _continuation, continuation) != null)
                throw new ArgumentException("Only one client can await this instance");
        }

        public void Enqueue(T item)
        {
            nextValue = item;
            var res = Interlocked.Exchange(ref _continuation, null);
            if (res != null)
                res();
        }


        public T GetResult()
        {
 Interlocked.Exchange(ref _currentValue, nextValue);
            return  _currentValue; 
        }

        public AwaitableQueue2<T> GetAwaiter() => this;

        public void Clear(Action<T> cleanUp)
        {
            cleanUp(_currentValue);
            cleanUp(nextValue);
        }
    }
}
