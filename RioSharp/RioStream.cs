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
        int remainingSpaceInOutputSegment = 0, currentContentLength = 0;
        private int OutputSegmentTotalLength;

        TaskCompletionSource<int> readtcs;
        byte[] readBuffer;
        int readoffset;
        int readCount;
        Action getNewSegmentDelegate;

        public RioStream(RioSocket socket)
        {
            _socket = socket;
            _currentInputSegment = null;
            _currentOutputSegment = _socket.SendBufferPool.GetBuffer();
            getNewSegmentDelegate = GetNewSegment;
        }

        public void Flush(bool moreData)
        {
            if (remainingSpaceInOutputSegment == 0)
                _socket.CommitSend();
            else if (remainingSpaceInOutputSegment == OutputSegmentTotalLength)
                return;
            else
            {
                unsafe
                {
                    _currentOutputSegment.segmentPointer->Length = OutputSegmentTotalLength - remainingSpaceInOutputSegment;
                }
                _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.NONE);

                if (moreData)
                {
                    _currentOutputSegment = _socket.SendBufferPool.GetBuffer();
                    OutputSegmentTotalLength = _currentOutputSegment.TotalLength;
                    remainingSpaceInOutputSegment = OutputSegmentTotalLength;
                }
                else
                {
                    remainingSpaceInOutputSegment = 0;
                    OutputSegmentTotalLength = 0;
                }

            }
        }

        public override void Flush()
        {
            Flush(true);
        }
        
        private void GetNewSegment()
        {
            _currentInputSegment = _socket.incommingSegments.GetResult();
            if (_currentInputSegment == null)
            {
                readtcs.SetResult(0);
                return;
            }

            _bytesReadInCurrentSegment = 0;
            currentContentLength = _currentInputSegment.CurrentContentLength;

            if (currentContentLength == 0)
            {
                _currentInputSegment.Dispose();
                _currentInputSegment = null;
                readtcs.SetResult(0);
                return;
            }
            else
            {
                _socket.ReciveInternal();
                CompleteRead();
            }
        }

        private void CompleteRead()
        {
            var toCopy = currentContentLength - _bytesReadInCurrentSegment;
            if (toCopy > readCount)
                toCopy = readCount;

            unsafe
            {
                fixed (byte* p = &readBuffer[readoffset])
                    Buffer.MemoryCopy(_currentInputSegment.rawPointer + _bytesReadInCurrentSegment, p, readCount, toCopy);
            }

            _bytesReadInCurrentSegment += toCopy;

            if (currentContentLength == _bytesReadInCurrentSegment)
            {
                _currentInputSegment.Dispose();
                _currentInputSegment = null;
            }

            readtcs.SetResult(toCopy);
        }
        
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            readtcs = new TaskCompletionSource<int>();
            readBuffer = buffer;
            readoffset = offset;
            readCount = count;

            if (_currentInputSegment == null)
            {
                if (_socket.incommingSegments.IsCompleted)
                    GetNewSegment();
                else
                    _socket.incommingSegments.OnCompleted(getNewSegmentDelegate);
            }
            else
                CompleteRead();

            return readtcs.Task;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
        }

        public override unsafe void Write(byte[] buffer, int offset, int count)
        {
            int writtenFromBuffer = 0;
            do
            {
                if (remainingSpaceInOutputSegment == 0)
                {
                    _currentOutputSegment.segmentPointer->Length = OutputSegmentTotalLength;
                    _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.DEFER); //| RIO_SEND_FLAGS.DONT_NOTIFY
                    while (!_socket.SendBufferPool.TryGetBuffer(out _currentOutputSegment))
                        _socket.CommitSend();
                    OutputSegmentTotalLength = _currentOutputSegment.TotalLength;
                    remainingSpaceInOutputSegment = OutputSegmentTotalLength;
                    continue;
                }

                var toWrite = count - writtenFromBuffer;
                if (toWrite > remainingSpaceInOutputSegment)
                    toWrite = remainingSpaceInOutputSegment;
                
                fixed (byte* p = &buffer[offset])
                {
                    Buffer.MemoryCopy(p + writtenFromBuffer, _currentOutputSegment.rawPointer + (OutputSegmentTotalLength - remainingSpaceInOutputSegment), remainingSpaceInOutputSegment, toWrite);
                }

                writtenFromBuffer += toWrite;
                remainingSpaceInOutputSegment -= toWrite;

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
