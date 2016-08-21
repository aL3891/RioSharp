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

        public RioSocket Socket
        {
            get
            {
                return _socket;
            }
            set
            {
                _socket = value;
            }
        }

        public RioSegmentReader(RioSocket socket)
        {
            _socket = socket;

            completeReadDelegate = CompleteReadOnThreadPool;
            completeReadWrapper = (o) => CompleteRead();
        }

        void CompleteReadOnThreadPool()
        {
            ThreadPool.QueueUserWorkItem(completeReadWrapper);
        }

        public void Start()
        {
            _nextInputSegment = _nextInputSegment ?? _socket.ReceiveBufferPool.GetBuffer();
            _currentInputSegment = _currentInputSegment ?? _socket.ReceiveBufferPool.GetBuffer();
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
            _currentInputSegment.GetResult();
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
            else {
                Dispose();
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
            _nextInputSegment.Dispose();
            _currentInputSegment.Dispose();
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
