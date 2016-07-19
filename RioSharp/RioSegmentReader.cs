using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioSegmentReader : IDisposable
    {
        RioSocket _socket;
        RioBufferSegment _currentInputSegment;
        RioBufferSegment _nextInputSegment = null;
        protected Action<RioBufferSegment> onIncommingSegment = s => { };
        Action completeReadDelegate;
        WaitCallback completeReadWrapper;

        public RioSegmentReader(RioSocket socket)
        {
            _socket = socket;
            _nextInputSegment = _socket.ReceiveBufferPool.GetBuffer();
            _currentInputSegment = _socket.ReceiveBufferPool.GetBuffer();
            completeReadDelegate = CompleteReadOnThreadPool;
            completeReadWrapper = (o) => CompleteRead();
        }

        void CompleteReadOnThreadPool()
        {
            ThreadPool.QueueUserWorkItem(completeReadWrapper);
        }

        public void Start()
        {
            _socket.BeginReceive(_nextInputSegment);

            if (_nextInputSegment.IsCompleted)
                CompleteRead();
            else
                _nextInputSegment.OnCompleted(completeReadDelegate);
        }

        void CompleteRead()
        {
            var tmp = _currentInputSegment;
            _currentInputSegment = _nextInputSegment;
            _nextInputSegment = tmp;
            if (_currentInputSegment.CurrentContentLength != 0)
                _socket.BeginReceive(_nextInputSegment);

            OnIncommingSegment(_currentInputSegment);

            if (_currentInputSegment.CurrentContentLength != 0)
            {
                if (_nextInputSegment.IsCompleted)
                    CompleteRead();
                else
                    _nextInputSegment.OnCompleted(completeReadDelegate);
            }
        }
        
        public Action<RioBufferSegment> OnIncommingSegment
        {
            get
            {
                return onIncommingSegment;
            }
            set
            {
                onIncommingSegment = value ?? (segment => { });
            }
        }

        public void Dispose()
        {
            _nextInputSegment.DisposeWhenComplete();
            _currentInputSegment.DisposeWhenComplete();
        }
    }


    public class RioSegmentReader<TState> : RioSegmentReader
    {
        Action<RioBufferSegment, TState> onIncommingSegmentState = (s, state) => { };
        public TState State { get; set; }

        public RioSegmentReader(RioSocket socket) : base(socket)
        {
            onIncommingSegment = Wrapper;
        }

        void Wrapper(RioBufferSegment segment)
        {
            onIncommingSegmentState(segment, State);
        }

        public new Action<RioBufferSegment, TState> OnIncommingSegment
        {
            get
            {
                return onIncommingSegmentState;
            }
            set
            {
                onIncommingSegmentState = value ?? ((segment, state) => { });
            }
        }
    }
}
