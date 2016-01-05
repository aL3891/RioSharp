using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public class _AwaitableQueue<T> : INotifyCompletion
    {
        ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        T _currentValue;
        Action _continuation = null;

        public bool IsCompleted => _queue.TryDequeue(out _currentValue);

        public void OnCompleted(Action continuation)
        {
            if (Interlocked.Exchange(ref _continuation, continuation) != null)
                throw new ArgumentException("Only one client can await this instance");
        }

        public void Enqueue(T item)
        {
            var res = Interlocked.Exchange(ref _continuation, null);
            if (res == null)
                _queue.Enqueue(item);
            else
            {
                _currentValue = item;
                res();
            }
        }

        public bool TryDequeue(out T item) => _queue.TryDequeue(out item);

        public T GetResult() => _currentValue;

        public _AwaitableQueue<T> GetAwaiter() => this;
    }
}
