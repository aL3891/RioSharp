using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace RioSharp
{
    public class RioTcpSocketStream : Stream
    {
        RioTcpSocket _socket;
        RioBufferSegment _currentInputSegment;
        RioBufferSegment _currentOutputSegment;
        int _bytesReadInCurrentSegment = 0;
        long _bytesWrittenInCurrentSegment = 0;

        public RioTcpSocketStream(RioTcpSocket socket)
        {
            _socket = socket;
            _currentInputSegment = null;
            _currentOutputSegment = _socket._pool.SendBufferPool.GetBuffer();
        }

        public void Flush(bool moreData)
        {
            if (_bytesWrittenInCurrentSegment == 0)
                _socket._pool.CommitSend(_socket._requestQueue);
            else
            {
                _socket._pool.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.NONE, _socket._requestQueue);
                if (moreData)
                {
                    _currentOutputSegment = _socket._pool.SendBufferPool.GetBuffer();
                    _bytesWrittenInCurrentSegment = 0;
                }
            }
        }

        public override void Flush()
        {
            Flush(true);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int readInCurrentRequest = 0;

            do
            {
                if (_currentInputSegment == null)
                {
                    if (!_socket.incommingSegments.TryReceive(out _currentInputSegment))
                    {
                        if (readInCurrentRequest != 0)
                        {
                            _currentInputSegment = null;
                            return (int)readInCurrentRequest;
                        }
                        else
                        {
                            _socket._pool.ReciveInternal(_socket._requestQueue);

                            try
                            {
                                _currentInputSegment = await _socket.incommingSegments.ReceiveAsync(cancellationToken);
                            }
                            catch (InvalidOperationException)
                            {
                                return 0;
                            }
                        }
                    }
                    _bytesReadInCurrentSegment = 0;
                }

                if (_currentInputSegment.CurrentLength == 0)
                    return 0;

                var toCopy = Math.Min(count, (int)_currentInputSegment.CurrentLength - _bytesReadInCurrentSegment);
                unsafe
                {
                    var pointer = (byte*)_currentInputSegment.Pointer.ToPointer();

                    fixed (byte* p = &buffer[0])
                    {
                        Buffer.MemoryCopy(pointer + _bytesReadInCurrentSegment,
                            p + offset + readInCurrentRequest,
                            count - readInCurrentRequest,
                            toCopy);
                    }
                    _bytesReadInCurrentSegment += toCopy;
                    readInCurrentRequest += toCopy;

                    if (_bytesReadInCurrentSegment == _currentInputSegment.CurrentLength)
                    {
                        _currentInputSegment.Dispose();
                        _currentInputSegment = null;
                    }
                }

            } while (readInCurrentRequest < count);

            return readInCurrentRequest;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
        }

        public override unsafe void Write(byte[] buffer, int offset, int count)
        {
            long remainingSpaceInSegment;
            var written = 0L;

            do
            {
                remainingSpaceInSegment = _socket._pool.SendBufferPool.SegmentLength - _bytesWrittenInCurrentSegment;
                if (remainingSpaceInSegment == 0)
                {
                    _socket._pool.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.DEFER | RIO_SEND_FLAGS.DONT_NOTIFY, _socket._requestQueue);
                    _currentOutputSegment = _socket._pool.SendBufferPool.GetBuffer();
                    _bytesWrittenInCurrentSegment = 0;
                    continue;
                }

                var toWrite = Math.Min(remainingSpaceInSegment, count - written);

                fixed (byte* p = &buffer[0])
                {
                    Buffer.MemoryCopy(p + offset + written, (byte*)_currentOutputSegment.Pointer.ToPointer() + _bytesWrittenInCurrentSegment, remainingSpaceInSegment, toWrite);
                }

                _bytesWrittenInCurrentSegment += (int)toWrite;
                written += toWrite;
            } while (written < count);
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
