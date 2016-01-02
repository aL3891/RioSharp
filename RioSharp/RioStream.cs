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
        uint remainingSpaceInOutputSegment =0;
        private uint OutputSegmentTotalLength;

        public RioStream(RioSocket socket)
        {
            _socket = socket;
            _currentInputSegment = null;
            _currentOutputSegment = _socket._pool.SendBufferPool.GetBuffer();
        }

        public void Flush(bool moreData)
        {
            if (remainingSpaceInOutputSegment == 0)
                _socket.CommitSend();
            else if (remainingSpaceInOutputSegment == OutputSegmentTotalLength)
                return;
            else
            {
                _currentOutputSegment.CurrentContentLength = OutputSegmentTotalLength - remainingSpaceInOutputSegment;
                _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.NONE);
                
                if (moreData)
                {
                    _currentOutputSegment = _socket._pool.SendBufferPool.GetBuffer();
                    OutputSegmentTotalLength = _currentOutputSegment.totalLength;
                    remainingSpaceInOutputSegment = OutputSegmentTotalLength;
                }
                else remainingSpaceInOutputSegment = 0;
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

                if (_currentInputSegment?.CurrentContentLength == 0)
                {
                    _currentInputSegment.Dispose();
                    _currentInputSegment = null;
                    return 0;
                }
                else
                    _socket.ReciveInternal();
            }

            var toCopy = Math.Min(count, (int)(_currentInputSegment.CurrentContentLength - _bytesReadInCurrentSegment));
            unsafe
            {
                fixed (byte* p = &buffer[offset])
                    Buffer.MemoryCopy(_currentInputSegment.rawPointer + _bytesReadInCurrentSegment, p, count, toCopy);
            }

            _bytesReadInCurrentSegment += toCopy;

            if (_currentInputSegment.CurrentContentLength == _bytesReadInCurrentSegment)
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
            long writtenFromBuffer = 0;
            do
            {
                if (remainingSpaceInOutputSegment == 0)
                {
                    _currentOutputSegment.CurrentContentLength = OutputSegmentTotalLength;
                    _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.DEFER ); //| RIO_SEND_FLAGS.DONT_NOTIFY
                    while (!_socket._pool.SendBufferPool.TryGetBuffer(out _currentOutputSegment))
                        _socket.CommitSend();
                    OutputSegmentTotalLength = _currentOutputSegment.totalLength;
                    remainingSpaceInOutputSegment = OutputSegmentTotalLength;
                    continue;
                }

                var toWrite = Math.Min(remainingSpaceInOutputSegment, count - writtenFromBuffer);

                fixed (byte* p = &buffer[offset])
                {
                    Buffer.MemoryCopy(p + writtenFromBuffer, _currentOutputSegment.rawPointer + (OutputSegmentTotalLength - remainingSpaceInOutputSegment), remainingSpaceInOutputSegment, toWrite);
                }

                writtenFromBuffer += toWrite;
                remainingSpaceInOutputSegment -= (uint)toWrite;

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
