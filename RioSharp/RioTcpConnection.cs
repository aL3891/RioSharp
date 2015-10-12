using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace RioSharp
{
    public class RioTcpConnection : Stream, IDisposable
    {
        IntPtr _socket;
        RioSocketPoolBase _pool;
        IntPtr _requestQueue;
        BufferSegment _currentInputSegment;
        uint _currentOutputSegment;
        long _bytesReadInCurrentSegment = 0;
        long _bytesWrittenInCurrentSegment = 0;

        internal BufferBlock<BufferSegment> incommingSegments = new BufferBlock<BufferSegment>();

        public RioTcpConnection(IntPtr socket, RioSocketPoolBase pool)
        {
            _socket = socket;
            _pool = pool;
            _requestQueue = RioStatic.CreateRequestQueue(_socket, _pool.MaxOutstandingReceive, 1, _pool.MaxOutstandingSend, 1, _pool.ReceiveCompletionQueue, _pool.SendCompletionQueue, GetHashCode());
            Imports.ThrowLastWSAError();
            _currentInputSegment = null;

        }

        public override void Close()
        {
            Imports.closesocket(_socket);
            Imports.ThrowLastWSAError();
            //destroy the queue?
        }

        public void WritePreAllocated(RIO_BUFSEGMENT Segment)
        {
            _pool.WritePreAllocated(Segment, _requestQueue);
        }

        public void WriteFixed(byte[] buffer)
        {
            _pool.WriteFixed(buffer, _requestQueue);
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            long readInCurrentRequest = 0;

            do
            {
                if (_currentInputSegment == null)
                {
                    if (!incommingSegments.TryReceive(out _currentInputSegment))
                    {
                        if (readInCurrentRequest != 0)
                        {
                            _currentInputSegment = null;
                            return (int)readInCurrentRequest;
                        }
                        else
                        {
                            _pool.ReciveInternal(_requestQueue);

                            try
                            {
                                _currentInputSegment = await incommingSegments.ReceiveAsync(cancellationToken);
                            }
                            catch (InvalidOperationException)
                            {
                                return 0;
                            }
                        }
                    }
                    _bytesReadInCurrentSegment = 0;
                }

                if (_currentInputSegment.Length == 0)
                    return 0;

                var toCopy = Math.Min(count, _currentInputSegment.Length - _bytesReadInCurrentSegment);
                unsafe
                {
                    var pointer = (byte*)_pool.ReciveBufferPool.BufferPointer.ToPointer() + _currentInputSegment.Segment;

                    fixed (byte* p = &buffer[0])
                    {
                        Buffer.MemoryCopy(pointer + _bytesReadInCurrentSegment,
                            p + offset + readInCurrentRequest,
                            count - readInCurrentRequest,
                            toCopy);
                    }
                    _bytesReadInCurrentSegment += toCopy;
                    readInCurrentRequest += toCopy;

                    if (_bytesReadInCurrentSegment == _currentInputSegment.Length)
                    {
                        _pool.ReciveBufferPool.ReleaseBuffer(_currentInputSegment.Segment);
                        _currentInputSegment = null;
                    }
                }

            } while (readInCurrentRequest < count);

            return (int)readInCurrentRequest;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
        }

        public unsafe override void Write(byte[] buffer, int offset, int count)
        {
            long remainingSpaceInSegment;
            var written = 0L;

            do
            {
                remainingSpaceInSegment = _pool.SendBufferPool.SegmentLength - _bytesWrittenInCurrentSegment;
                if (remainingSpaceInSegment == 0)
                {
                    _pool.SendInternal(_currentOutputSegment, (uint)_bytesWrittenInCurrentSegment, RIO_SEND_FLAGS.DEFER | RIO_SEND_FLAGS.DONT_NOTIFY, _requestQueue);
                    _currentOutputSegment = _pool.SendBufferPool.GetBuffer();
                    _bytesWrittenInCurrentSegment = 0;
                    continue;
                }

                var toWrite = Math.Min(remainingSpaceInSegment, count - written);

                fixed (byte* p = &buffer[0])
                {
                    Buffer.MemoryCopy(p + offset + written, (byte*)_pool.SendBufferPool.BufferPointer.ToPointer() + _bytesWrittenInCurrentSegment + _currentOutputSegment, remainingSpaceInSegment, toWrite);
                }

                _bytesWrittenInCurrentSegment += (int)toWrite;
                written += toWrite;
            } while (written < count);
        }

        protected override void Dispose(bool disposing)
        {
            Flush(false);
            _pool.ReciveBufferPool.ReleaseBuffer(_currentInputSegment.Segment);
            incommingSegments.Complete();
            IList<BufferSegment> segments;
            incommingSegments.TryReceiveAll(out segments);

            foreach (var s in segments)
                _pool.ReciveBufferPool.ReleaseBuffer(s.Segment);

            _pool.SendBufferPool.ReleaseBuffer(_currentOutputSegment);
            base.Dispose(disposing);

        }

        public void Flush(bool moreData)
        {
            if (_bytesWrittenInCurrentSegment == 0)
                _pool.CommitSend(_requestQueue);
            else
            {
                _pool.SendInternal(_currentOutputSegment, (uint)_bytesWrittenInCurrentSegment, RIO_SEND_FLAGS.NONE, _requestQueue);
            }

            if (moreData)
            {
                _currentOutputSegment = _pool.SendBufferPool.GetBuffer();
                _bytesWrittenInCurrentSegment = 0;
            }
        }

        public override void Flush()
        {
            Flush(true);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length { get { throw new NotImplementedException(); } }
        public override long Position { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { return 0; }
        public override void SetLength(long value) { throw new NotImplementedException(); }
    }

    public class BufferSegment
    {
        public BufferSegment(uint segment, uint length)
        {
            Segment = segment;
            Length = length;
        }

        internal uint Segment;
        internal uint Length;
    }
}
