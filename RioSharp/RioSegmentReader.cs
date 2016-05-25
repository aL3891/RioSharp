using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioSegmentReader : IDisposable
    {
        RioSocket _socket;
        RioBufferSegment _currentInputSegment;
        RioBufferSegment _nextInputSegment = null;
        internal Action<RioBufferSegment> onIncommingSegment = s => { };
        Action completeReadDelegate;

        public RioSegmentReader(RioSocket socket)
        {
            _socket = socket;
            _nextInputSegment = _socket.ReceiveBufferPool.GetBuffer();
            _currentInputSegment = _socket.ReceiveBufferPool.GetBuffer();            
            completeReadDelegate = CompleteRead;
        }
        
        public void Start()
        {
            _socket.BeginReceive(_nextInputSegment);

            if (_nextInputSegment.IsCompleted)
                CompleteRead();
            else
                _nextInputSegment.OnCompleted(completeReadDelegate);
        }
        
        public void CompleteRead()
        {
            var tmp = _currentInputSegment;
            _currentInputSegment = _nextInputSegment;
            _nextInputSegment = tmp;
            
            _socket.BeginReceive(_nextInputSegment);

            OnIncommingSegment(_currentInputSegment);

            if (_nextInputSegment.IsCompleted)
                CompleteRead();
            else
                _nextInputSegment.OnCompleted(completeReadDelegate);

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
            
        }
    }
}
