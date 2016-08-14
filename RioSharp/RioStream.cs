using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace RioSharp
{
    public class RioStream : Stream
    {
        RioSocket _socket;
        RioBufferSegment _currentInputSegment;
        RioBufferSegment _nextInputSegment = null;
        RioBufferSegment _currentOutputSegment;
        int _bytesReadInCurrentSegment = 0;
        int _remainingSpaceInOutputSegment = 0, _currentContentLength = 0;
        int _outputSegmentTotalLength;
        TaskCompletionSource<int> _readtcs;
        byte[] _readBuffer;
        int _readoffset;
        int _readCount;
        Action _getNewSegmentDelegateDelegate;
        WaitCallback _waitCallback;
        bool disposing = false;

        public RioStream(RioSocket socket)
        {
            _socket = socket;
            _currentInputSegment = _socket.ReceiveBufferPool.GetBuffer();
            _currentOutputSegment = _socket.SendBufferPool.GetBuffer();
            _nextInputSegment = _socket.ReceiveBufferPool.GetBuffer();
            _getNewSegmentDelegateDelegate = GetNewSegmentDelegateWrapper;

            _outputSegmentTotalLength = _currentOutputSegment.TotalLength;
            _remainingSpaceInOutputSegment = _outputSegmentTotalLength;
            _socket.BeginReceive(_nextInputSegment);

            _waitCallback = WaitCallbackcallback;
        }

        public void Flush(bool moreData)
        {
            if (_remainingSpaceInOutputSegment == 0)
                _socket.Flush();
            else if (_remainingSpaceInOutputSegment == _outputSegmentTotalLength)
                return;
            else
            {
                unsafe
                {
                    _currentOutputSegment.SegmentPointer->Length = _outputSegmentTotalLength - _remainingSpaceInOutputSegment;
                }
                _socket.Send(_currentOutputSegment, RIO_SEND_FLAGS.NONE);
                _currentOutputSegment.DisposeWhenComplete();
                if (moreData)
                {
                    _currentOutputSegment = _socket.SendBufferPool.GetBuffer();
                    _outputSegmentTotalLength = _currentOutputSegment.TotalLength;
                    _remainingSpaceInOutputSegment = _outputSegmentTotalLength;
                }
                else
                {
                    _remainingSpaceInOutputSegment = 0;
                    _outputSegmentTotalLength = 0;
                }
            }
        }

        public override void Flush()
        {
            Flush(true);
        }

        int GetNewSegment()
        {
            _currentContentLength = _nextInputSegment.CurrentContentLength;

            if (disposing || _currentContentLength == 0)
            {
                _nextInputSegment.Dispose();
                _currentInputSegment.Dispose();
                return 0;
            }
            else
            {
                _bytesReadInCurrentSegment = 0;
                _nextInputSegment = _socket.BeginReceive(Interlocked.Exchange(ref _currentInputSegment, _nextInputSegment));
                return CompleteRead();
            }
        }

        int CompleteRead()
        {
            var toCopy = _currentContentLength - _bytesReadInCurrentSegment;
            if (toCopy > _readCount)
                toCopy = _readCount;

            unsafe
            {
                fixed (byte* p = &_readBuffer[_readoffset])
                    Unsafe.CopyBlock(p, _currentInputSegment.dataPointer + _bytesReadInCurrentSegment, (uint)toCopy);
            }

            _bytesReadInCurrentSegment += toCopy;

            return toCopy;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _readBuffer = buffer;
            _readoffset = offset;
            _readCount = count;

            if (_currentContentLength == _bytesReadInCurrentSegment)
            {
                if (_nextInputSegment.IsCompleted)
                    return Task.FromResult(GetNewSegment());
                else
                {
                    _readtcs = new TaskCompletionSource<int>();
                    _nextInputSegment.OnCompleted(_getNewSegmentDelegateDelegate);
                    return _readtcs.Task;
                }
            }
            else
                return Task.FromResult(CompleteRead());

        }

        void WaitCallbackcallback(object o)
        {
            if (_readtcs.TrySetResult(GetNewSegment()))
            {
            }
            else {

            }
        }

        void GetNewSegmentDelegateWrapper()
        {
            if (_readtcs.Task.IsCompleted) {

            }
            ThreadPool.QueueUserWorkItem(_waitCallback);
        }

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, CancellationToken.None).Result;

        public override unsafe void Write(byte[] buffer, int offset, int count)
        {
            int writtenFromBuffer = 0;
            do
            {
                if (_remainingSpaceInOutputSegment == 0)
                {
                    var tmp = Interlocked.Exchange(ref _currentOutputSegment, null);
                    tmp.SegmentPointer->Length = _outputSegmentTotalLength;
                    _socket.Send(tmp, RIO_SEND_FLAGS.DEFER); // | RIO_SEND_FLAGS.DONT_NOTIFY
                    tmp.DisposeWhenComplete();
                    while (!_socket.SendBufferPool.TryGetBuffer(out _currentOutputSegment))
                        _socket.Flush();
                    _outputSegmentTotalLength = _currentOutputSegment.TotalLength;
                    _remainingSpaceInOutputSegment = _outputSegmentTotalLength;
                    continue;
                }

                var toWrite = count - writtenFromBuffer;
                if (toWrite > _remainingSpaceInOutputSegment)
                    toWrite = _remainingSpaceInOutputSegment;

                fixed (byte* p = &buffer[offset])
                {
                    Unsafe.CopyBlock(_currentOutputSegment.dataPointer + (_outputSegmentTotalLength - _remainingSpaceInOutputSegment), p + writtenFromBuffer, (uint)toWrite);
                }

                writtenFromBuffer += toWrite;
                _remainingSpaceInOutputSegment -= toWrite;

            } while (writtenFromBuffer < count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposing)
                return;
            this.disposing = true;

            Flush(false);
            
            

            _currentOutputSegment?.Dispose();
            //_currentInputSegment?.Dispose();

            if (!_nextInputSegment.IsAwaited)
            {
                _nextInputSegment?.DisposeWhenComplete();
                _currentInputSegment?.Dispose();
            }

        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length { get { throw new NotImplementedException(); } }
        public override long Position { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { return 0; }
        public override void SetLength(long value) { throw new NotImplementedException(); }

    }

    internal class PoolingLinkedList<T>
    {
        Node _first = null, _last = null;
        Node _firstFree = null, _lastFree = null;

        public void Push(object value)
        {
            var n = Interlocked.Exchange(ref _firstFree, _firstFree.Next);
            var l = Interlocked.Exchange(ref _last.Next, n);
            while (l != null)
                l = Interlocked.Exchange(ref _last.Next, n);
        }

        public T Pop()
        {
            T res;
            var r = Interlocked.Exchange(ref _first, _first.Next);
            res = r.value;
            var l = Interlocked.Exchange(ref _lastFree.Next, r);
            while (l != null)
                l = Interlocked.Exchange(ref _lastFree.Next, r);

            return res;
        }

        public class Node
        {
            public Node Next = null;
            public Node prev = null;
            public T value = default(T);
        }
    }
}
