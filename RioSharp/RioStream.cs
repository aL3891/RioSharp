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
        private Action _getNewSegmentDelegateDelegate;
        WaitCallback _waitCallback;

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
                _socket.CommitSend();
            else if (_remainingSpaceInOutputSegment == _outputSegmentTotalLength)
                return;
            else
            {
                unsafe
                {
                    _currentOutputSegment.SegmentPointer->Length = _outputSegmentTotalLength - _remainingSpaceInOutputSegment;
                }
                _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.NONE);
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

        private int GetNewSegment()
        {
            var tmp = _currentInputSegment;
            _nextInputSegment.GetResult();
            _currentInputSegment = _nextInputSegment;

            _bytesReadInCurrentSegment = 0;
            _currentContentLength = _currentInputSegment.CurrentContentLength;

            if (_currentContentLength == 0)
            {
                _currentInputSegment.Dispose();
                tmp.Dispose();
                return 0;
            }
            else
            {
                _nextInputSegment = tmp;
                _socket.BeginReceive(_nextInputSegment);
                return CompleteRead();
            }
        }

        private int CompleteRead()
        {
            var toCopy = _currentContentLength - _bytesReadInCurrentSegment;
            if (toCopy > _readCount)
                toCopy = _readCount;

            unsafe
            {
                fixed (byte* p = &_readBuffer[_readoffset])
                    Unsafe.CopyBlock(p, _currentInputSegment.RawPointer + _bytesReadInCurrentSegment, (uint)toCopy);
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

        private void WaitCallbackcallback(object o)
        {
            _readtcs.SetResult(GetNewSegment());
        }

        private void GetNewSegmentDelegateWrapper()
        {
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
                    _currentOutputSegment.SegmentPointer->Length = _outputSegmentTotalLength;
                    _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.DEFER); // | RIO_SEND_FLAGS.DONT_NOTIFY
                    _currentOutputSegment.DisposeWhenComplete();
                    while (!_socket.SendBufferPool.TryGetBuffer(out _currentOutputSegment))
                        _socket.CommitSend();
                    _outputSegmentTotalLength = _currentOutputSegment.TotalLength;
                    _remainingSpaceInOutputSegment = _outputSegmentTotalLength;
                    continue;
                }

                var toWrite = count - writtenFromBuffer;
                if (toWrite > _remainingSpaceInOutputSegment)
                    toWrite = _remainingSpaceInOutputSegment;

                fixed (byte* p = &buffer[offset])
                {
                    Unsafe.CopyBlock(_currentOutputSegment.RawPointer + (_outputSegmentTotalLength - _remainingSpaceInOutputSegment), p + writtenFromBuffer, (uint)toWrite);
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
            Flush(false);

            _currentInputSegment?.Dispose();
            _currentOutputSegment?.Dispose();
            _nextInputSegment?.Dispose();
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
