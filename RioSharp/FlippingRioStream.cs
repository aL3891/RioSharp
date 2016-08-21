using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace RioSharp
{
    internal class FlippingRioStream : Stream
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

        public FlippingRioStream(RioSocket socket)
        {
            _socket = socket;
            _currentInputSegment = _socket.ReceiveBufferPool.GetBuffer();
            _currentOutputSegment = _socket.SendBufferPool.GetBuffer();
            _nextInputSegment = _socket.ReceiveBufferPool.GetBuffer();

            if (_nextInputSegment._awaitableState != RioBufferSegment._notStarted)
            {

            }

            _getNewSegmentDelegateDelegate = GetNewSegmentDelegateWrapper;

            _outputSegmentTotalLength = _currentOutputSegment.TotalLength;
            _remainingSpaceInOutputSegment = _outputSegmentTotalLength;
            _socket.BeginReceive(_nextInputSegment);

            _waitCallback = WaitCallbackcallback;
        }


        void Flush(bool disposing)
        {
            if (_remainingSpaceInOutputSegment == 0)
                _socket.Flush();
            else if (_remainingSpaceInOutputSegment == _outputSegmentTotalLength)
            {
                if (disposing)
                    _currentOutputSegment.Dispose();
            }
            else
            {
                unsafe
                {
                    _currentOutputSegment.SegmentPointer->Length = _outputSegmentTotalLength - _remainingSpaceInOutputSegment;
                }
                _socket.Send(_currentOutputSegment, RIO_SEND_FLAGS.NONE);
                _currentOutputSegment.Dispose();
                if (disposing)
                {
                    _remainingSpaceInOutputSegment = 0;
                    _outputSegmentTotalLength = 0;
                }
                else
                {
                    _currentOutputSegment = _socket.SendBufferPool.GetBuffer();
                    _outputSegmentTotalLength = _currentOutputSegment.TotalLength;
                    _remainingSpaceInOutputSegment = _outputSegmentTotalLength;
                }
            }
        }

        public override void Flush()
        {
            Flush(false);
        }

        int GetNewSegment()
        {
            _nextInputSegment.GetResult();
            _currentContentLength = _nextInputSegment.CurrentContentLength;
            if (disposing || _currentContentLength == 0)
            {
                _nextInputSegment.Dispose();
                _currentInputSegment.Dispose();
                toCopy = 0;
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
            toCopy = _currentContentLength - _bytesReadInCurrentSegment;
            if (toCopy > _readCount)
                toCopy = _readCount;

            unsafe
            {
                fixed (byte* p = &_readBuffer[_readoffset])
                    Unsafe.CopyBlock(p, _currentInputSegment.dataPointer + _bytesReadInCurrentSegment, (uint)toCopy);
            }

            _bytesReadInCurrentSegment += toCopy;


            Interlocked.Decrement(ref pendingreads);
            return toCopy;
        }

        int pendingreads = 0;
        private int toCopy;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {

            Interlocked.Increment(ref pendingreads);

            if (pendingreads > 1)
            {

            }
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
            //dispose hinner inte färdigt av nån anledning


            _readtcs.SetResult(toCopy);
        }

        void GetNewSegmentDelegateWrapper()
        {
            if (_readtcs.Task.IsCompleted)
            {

            }
            GetNewSegment();
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
                    tmp.Dispose();
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

            Flush(true);
            _nextInputSegment?.Dispose();
            _currentInputSegment?.Dispose();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length { get { throw new NotImplementedException(); } }
        public override long Position { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { return 0; }
        public override void SetLength(long value) { throw new NotImplementedException(); }

    }
    
}
