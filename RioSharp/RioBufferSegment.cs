using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        internal static readonly Action _completed = () => { Debug.Assert(false); };
        internal static readonly Action _completing = () => { Debug.Assert(false); };
        internal static readonly Action _pending = () => { Debug.Assert(false); };
        internal static readonly Action _notStarted = () => { Debug.Assert(false); };
        internal static readonly Action _disposePending = () => { Debug.Assert(false); };
        internal static readonly Action _disposeReady = () => { Debug.Assert(false); };

        static readonly Action<decimal> emptyCompletion = id => { };

        internal Action _awaitableState;
        internal Action<decimal> _internalCompletionSignal = emptyCompletion;
        ManualResetEventSlim _blockingEvent = new ManualResetEventSlim(false, 0);
        ManualResetEventSlim _disposeEvent = new ManualResetEventSlim(false, 0);
        ManualResetEventSlim _completeEvent = new ManualResetEventSlim(false, 0);
        Exception _awaitableError;
        internal RioSocket lastSocket = null;

        RioFixedBufferPool _pool;
        internal int Index;
        internal int TotalLength;
        public int CurrentContentLength => SegmentPointer->Length;
        internal decimal socketId;

        internal byte* dataPointer;
        internal RIO_BUF* SegmentPointer;
        internal bool InUse = false;
        WaitCallback _continuationWrapperDelegate;
        private Action pendingContinuation;

        public byte* DataPointer => dataPointer;

        public unsafe int Read(byte[] data, int offset)
        {
            Debug.Assert(InUse);
            var count = Math.Min((data.Length - offset), CurrentContentLength);
            fixed (void* p = &data[0])
                Unsafe.CopyBlock(p, dataPointer, (uint)count);
            return count;
        }

        public unsafe int Write(byte[] data)
        {
            Debug.Assert(InUse);
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
            _continuationWrapperDelegate = o => ((Action)o)();

            _awaitableState = _notStarted;
        }

        internal void SetBufferId(IntPtr id)
        {
            SegmentPointer->BufferId = id;
        }

        public void Dispose()
        {
            Debug.Assert(InUse);
            
            var res = Interlocked.Exchange(ref _awaitableState, _disposePending);

            if (ReferenceEquals(res, _completed))
                Disposeinternal();
            else if (ReferenceEquals(res, _notStarted))
                Disposeinternal();
            else if (ReferenceEquals(res, _disposePending))
                return;
            else if (ReferenceEquals(res, _disposeReady))
                return;
            else if (ReferenceEquals(res, _completing))
            {
                _completeEvent.Wait();
                Disposeinternal();
            }
            else if (ReferenceEquals(res, _pending))
            {
                pendingContinuation = null;
                Interlocked.Exchange(ref _awaitableState, _disposeReady);
                _disposeEvent.Set();
            }
            else
            {
                pendingContinuation = res;
                Interlocked.Exchange(ref _awaitableState, _disposeReady);
                _disposeEvent.Set();
            }

        }

        internal void SetNotComplete()
        {
            Debug.Assert(InUse);
            Interlocked.Exchange(ref _awaitableState, _pending);
            _blockingEvent.Reset();
            _completeEvent.Reset();
        }

        internal void DisposeOnComplete()
        {
            Debug.Assert(InUse);
            Interlocked.Exchange(ref _awaitableState, _disposeReady);
            _blockingEvent.Reset();
            _completeEvent.Reset();
        }

        void Disposeinternal()
        {
            Debug.Assert(InUse);
            pendingContinuation = null;
            Interlocked.Exchange(ref _awaitableState, _notStarted);
            _internalCompletionSignal = emptyCompletion;
            SegmentPointer->Length = 0;
            _disposeEvent.Set();
            _blockingEvent.Set();
            _completeEvent.Set();
            _pool.ReleaseBuffer(this);
        }

        public bool IsCompleted => ReferenceEquals(_awaitableState, _completed);

        public void Set()
        {
            Debug.Assert(InUse);
            var state = Interlocked.Exchange(ref _awaitableState, _completing);
            _internalCompletionSignal(socketId);
            _blockingEvent.Set();

            if (ReferenceEquals(state, _disposePending))
                _disposeEvent.Wait();

            if (ReferenceEquals(state, _disposeReady))
            {
                if (pendingContinuation != null)
                {
                    _awaitableError = new ObjectDisposedException("dizpizzled");
                    ThreadPool.QueueUserWorkItem(o => pendingContinuation());
                }
                else
                    Disposeinternal();
            }
            else if (
                !ReferenceEquals(state, _completed) &&
                !ReferenceEquals(state, _completing) &&
                !ReferenceEquals(state, _disposePending) &&
                !ReferenceEquals(state, _disposeReady) &&
                !ReferenceEquals(state, _pending) &&
                !ReferenceEquals(state, _notStarted))
            {
                state();
            }
            else
            {
                Interlocked.Exchange(ref _awaitableState, _completed);
                _completeEvent.Set();
            }
        }

        public void OnCompleted(Action continuation)
        {
            Debug.Assert(InUse);
            var awaitableState = Interlocked.CompareExchange(ref _awaitableState, continuation, _pending);

            if (ReferenceEquals(awaitableState, _pending))
                return;
            else if (ReferenceEquals(awaitableState, _notStarted))
                new InvalidOperationException("Can't wait for unstarted operation");
            else if (ReferenceEquals(awaitableState, _completed))
                ThreadPool.QueueUserWorkItem(o => continuation());
            else if (ReferenceEquals(awaitableState, _completing))
            {
                _completeEvent.Wait();
                continuation();
            }
            else if (ReferenceEquals(awaitableState, _disposePending))
            {
                _disposeEvent.Wait();

                if (pendingContinuation != null)
                {
                    _awaitableError = new ObjectDisposedException("dizpizzled");
                    ThreadPool.QueueUserWorkItem(o => continuation());
                    ThreadPool.QueueUserWorkItem(o => awaitableState());
                }
                else
                {
                    pendingContinuation = continuation;
                }
            }
            else
            {
                _awaitableError = new InvalidOperationException("Concurrent operations are not supported.");

                Interlocked.Exchange(ref _awaitableState, _completed);
                _blockingEvent.Set();

                ThreadPool.QueueUserWorkItem(o => continuation());
                ThreadPool.QueueUserWorkItem(o => awaitableState());
            }
        }

        public RioBufferSegment GetResult()
        {
            Debug.Assert(InUse);
            if (!IsCompleted)
                _blockingEvent.Wait();

            Interlocked.Exchange(ref _awaitableState, _completed);
            _completeEvent.Set();

            var error = _awaitableError;
            if (error != null)
                throw error;

            return this;
        }

        public RioBufferSegment GetAwaiter() => this;
    }
}
