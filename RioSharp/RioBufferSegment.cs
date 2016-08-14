using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public sealed unsafe class RioBufferSegment : IDisposable, INotifyCompletion
    {
        static readonly Action _awaitableIsCompleted = () => { };
        static readonly Action _awaitableIsNotCompleted = () => { };
        static readonly Action<decimal> emptyCompletion = id => { };

        Action _awaitableState;
        internal Action<decimal> _internalCompletionSignal = emptyCompletion;
        ManualResetEventSlim _manualResetEvent = new ManualResetEventSlim(false, 0);
        Exception _awaitableError;
        internal RioSocket lastSocket;

        RioFixedBufferPool _pool;
        internal int Index;
        internal int TotalLength;
        public int CurrentContentLength => SegmentPointer->Length;
        internal decimal socketId;

        internal byte* dataPointer;
        internal RIO_BUF* SegmentPointer;

        bool disposeOnComplete = false;
        WaitCallback _continuationWrapperDelegate;

        public byte* DataPointer => dataPointer;

        public unsafe int Read(byte[] data, int offset)
        {
            var count = Math.Min((data.Length - offset), CurrentContentLength);

            fixed (void* p = &data[0])
                Unsafe.CopyBlock(p, dataPointer, (uint)count);

            return count;
        }

        public unsafe int Write(byte[] data)
        {
            var count = Math.Min((data.Length), TotalLength - SegmentPointer->Length);

            fixed (void* p = &data[0])
                Unsafe.CopyBlock(dataPointer + SegmentPointer->Length, p, (uint)count);

            SegmentPointer->Length += count;

            return count;
        }

        internal RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, int index, int Length)
        {
            Index = index;
            TotalLength = Length;
            _pool = pool;

            var offset = index * Length;
            dataPointer = (byte*)(bufferStartPointer + offset).ToPointer();
            SegmentPointer = (RIO_BUF*)(segmentStartPointer + index * Marshal.SizeOf<RIO_BUF>()).ToPointer();

            SegmentPointer->BufferId = IntPtr.Zero;
            SegmentPointer->Offset = offset;
            SegmentPointer->Length = 0;
            _awaitableState = _awaitableIsNotCompleted;

            _continuationWrapperDelegate = o => ((Action)o)();
        }

        internal void SetBufferId(IntPtr id)
        {
            SegmentPointer->BufferId = id;
        }

        public void DisposeWhenComplete()
        {
            disposeOnComplete = !ReferenceEquals(_awaitableState, _awaitableIsCompleted);

            if (!disposeOnComplete)
                Dispose();
            else
            {
            }
        }

        internal void SetNotComplete()
        {
            Interlocked.Exchange(ref _awaitableState, _awaitableIsNotCompleted);
            _manualResetEvent.Reset();
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _awaitableState, _awaitableIsNotCompleted);
            _manualResetEvent.Reset();
            disposeOnComplete = false;
            _internalCompletionSignal = emptyCompletion;
            SegmentPointer->Length = 0;
            _pool.ReleaseBuffer(this);
        }

        public bool IsCompleted => ReferenceEquals(_awaitableState, _awaitableIsCompleted);

        public bool IsAwaited => !ReferenceEquals(_awaitableState, _awaitableIsCompleted) && !ReferenceEquals(_awaitableState, _awaitableIsNotCompleted);

        public void Set()
        {
            var awaitableState = Interlocked.Exchange(ref _awaitableState, _awaitableIsCompleted);
            _internalCompletionSignal(socketId);
            _manualResetEvent.Set();

            if (!ReferenceEquals(awaitableState, _awaitableIsCompleted) &&
                !ReferenceEquals(awaitableState, _awaitableIsNotCompleted))
            {
                awaitableState();
            }

            if (disposeOnComplete)
                Dispose();
            else
            {
            }
        }

        public void OnCompleted(Action continuation)
        {
            var awaitableState = Interlocked.CompareExchange(ref _awaitableState, continuation, _awaitableIsNotCompleted);

            if (ReferenceEquals(awaitableState, _awaitableIsNotCompleted))
                return;

            else if (ReferenceEquals(awaitableState, _awaitableIsCompleted))
                ThreadPool.QueueUserWorkItem(o => continuation());
            else
            {
                _awaitableError = new InvalidOperationException("Concurrent operations are not supported.");

                Interlocked.Exchange(ref _awaitableState, _awaitableIsCompleted);
                _manualResetEvent.Set();

                ThreadPool.QueueUserWorkItem(o => continuation());
                ThreadPool.QueueUserWorkItem(o => awaitableState());
            }
        }

        public RioBufferSegment GetResult()
        {
            if (!IsCompleted)
            {
                _manualResetEvent.Wait();
            }
            var error = _awaitableError;
            if (error != null)
            {
                if (error is TaskCanceledException || error is InvalidOperationException)
                {
                    throw error;
                }
                throw new IOException(error.Message, error);
            }
            return this;
        }

        public RioBufferSegment GetAwaiter() => this;
    }
}
