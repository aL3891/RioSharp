using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    public interface  IAbstractThreadPool
    {
        void Enqueue(Action action);
        void Enqueue<T>(Action<T> action, T arg);

    }
}
