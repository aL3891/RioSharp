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
    public sealed unsafe class RioBufferSegment : IDisposable
    {
        private static readonly Action _awaitableIsCompleted = () => { };
        private static readonly Action _awaitableIsNotCompleted = () => { };

        private Action _awaitableState;
        private readonly ManualResetEventSlim _manualResetEvent = new ManualResetEventSlim(false, 0);
        private Exception _awaitableError;
        internal RioSocket lastSocket;

        RioFixedBufferPool _pool;
        internal int Index;
        internal int TotalLength;
        public int CurrentContentLength => SegmentPointer->Length;

        internal byte* RawPointer;
        internal RIO_BUF* SegmentPointer;

        bool disposeOnComplete = false;
        WaitCallback _continuationWrapperDelegate;

        public byte* Datapointer => RawPointer;

        public unsafe int Read(byte[] data, int offset)
        {
            var l = Math.Min((data.Length - offset), CurrentContentLength);

            fixed (void* p = &data[0])
                Unsafe.CopyBlock(p, RawPointer, (uint)l);

            return l;
        }

        public unsafe int Write(byte[] data)
        {
            var l = Math.Min((data.Length), TotalLength - SegmentPointer->Length);

            fixed (void* p = &data[0])
                Unsafe.CopyBlock(RawPointer + SegmentPointer->Length, p, (uint)l);

            SegmentPointer->Length += l;

            return l;
        }

        internal RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, int index, int Length)
        {
            Index = index;
            TotalLength = Length;
            _pool = pool;

            var offset = index * Length;
            RawPointer = (byte*)(bufferStartPointer + offset).ToPointer();
            SegmentPointer = (RIO_BUF*)(segmentStartPointer + index * Marshal.SizeOf<RIO_BUF>()).ToPointer();

            SegmentPointer->BufferId = IntPtr.Zero;
            SegmentPointer->Offset = offset;
            SegmentPointer->Length = 0;

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
            SegmentPointer->Length = 0;
            _pool.ReleaseBuffer(this);
        }

        public bool IsCompleted => ReferenceEquals(_awaitableState, _awaitableIsCompleted);

        public void Set()
        {
            var awaitableState = Interlocked.Exchange(ref _awaitableState, _awaitableIsCompleted);
            _manualResetEvent.Set();

            if (!ReferenceEquals(awaitableState, _awaitableIsCompleted) &&
                !ReferenceEquals(awaitableState, _awaitableIsNotCompleted))
            {
                awaitableState();
            }

            if (disposeOnComplete)
                Dispose();
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

        public void GetResult()
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
        }

        public RioBufferSegment GetAwaiter() => this;
    }
}
