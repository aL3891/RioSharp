using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace RioSharp
{
    public class RioStream : Stream
    {
        RioSocket _socket;
        RioBufferSegment _currentInputSegment;
        RioBufferSegment _currentOutputSegment;
        int _bytesReadInCurrentSegment = 0;

        public RioStream(RioSocket socket)
        {
            _socket = socket;
            _currentInputSegment = null;
            _currentOutputSegment = _socket._pool.SendBufferPool.GetBuffer();
        }

        public void Flush(bool moreData)
        {
            if (_currentOutputSegment.RemainingSpace == 0)
                _socket.CommitSend();
            else
            {
                _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.NONE);
                if (moreData)
                {
                    _currentOutputSegment = _socket._pool.SendBufferPool.GetBuffer();
                }
            }
        }

        public override void Flush()
        {
            Flush(true);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_currentInputSegment == null)
            {
                _currentInputSegment = await _socket.incommingSegments;
                _bytesReadInCurrentSegment = 0;

                if (_currentInputSegment?.ContentLength == 0)
                {
                    _currentInputSegment.Dispose();
                    _currentInputSegment = null;
                    return 0;
                }
                else
                    _socket.ReciveInternal();
            }

            var toCopy = Math.Min(count, (int)(_currentInputSegment.ContentLength - _bytesReadInCurrentSegment));
            unsafe
            {
                var pointer = (byte*)_currentInputSegment.Pointer.ToPointer();

                fixed (byte* p = &buffer[offset])
                    Buffer.MemoryCopy(pointer + _bytesReadInCurrentSegment, p, count, toCopy);
            }

            _bytesReadInCurrentSegment += toCopy;

            if (_currentInputSegment.ContentLength == _bytesReadInCurrentSegment)
            {
                _currentInputSegment.Dispose();
                _currentInputSegment = null;
            }

            return toCopy;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
        }

        public override unsafe void Write(byte[] buffer, int offset, int count)
        {
            uint remainingSpaceInSegment;
            long writtenFromBuffer = 0;

            do
            {
                remainingSpaceInSegment = _currentOutputSegment.RemainingSpace;
                if (remainingSpaceInSegment == 0)
                {
                    _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.DEFER | RIO_SEND_FLAGS.DONT_NOTIFY);
                    _currentOutputSegment = _socket._pool.SendBufferPool.GetBuffer();
                    continue;
                }

                var toWrite = Math.Min(remainingSpaceInSegment, count - writtenFromBuffer);

                fixed (byte* p = &buffer[offset])
                {
                    Buffer.MemoryCopy(p + writtenFromBuffer, (byte*)_currentOutputSegment.Pointer.ToPointer() + _currentOutputSegment.ContentLength, remainingSpaceInSegment, toWrite);
                }

                _currentOutputSegment.ContentLength += (uint)toWrite;
                writtenFromBuffer += toWrite;
            } while (writtenFromBuffer < count);
        }

        protected override void Dispose(bool disposing)
        {
            Flush(false);

            if (_currentInputSegment != null)
                _currentInputSegment.Dispose();

            _currentOutputSegment.Dispose();
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
