using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public sealed unsafe class RioBufferSegment : IDisposable
    {
        RioFixedBufferPool _pool;
        internal int Index;
        internal int TotalLength;
        public int CurrentContentLength => SegmentPointer->Length;

        internal byte* RawPointer;
        internal RIO_BUFSEGMENT* SegmentPointer;
        internal bool complete;
        Action _continuation;
        SpinLock _spinLock = new SpinLock();
        bool disposeOnComplete = false;
        WaitCallback _continuationWrapperDelegate;

        public byte* Datapointer => RawPointer;

        public int GetData(byte[] data, int offset)
        {
            var l = Math.Min((data.Length - offset), CurrentContentLength);
            Marshal.Copy(new IntPtr(RawPointer), data, offset, l);
            return l;
        }

        internal RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, int index, int Length)
        {
            Index = index;
            TotalLength = Length;
            _pool = pool;

            var offset = index * Length;
            RawPointer = (byte*)(bufferStartPointer + offset).ToPointer();
            SegmentPointer = (RIO_BUFSEGMENT*)(segmentStartPointer + index * Marshal.SizeOf<RIO_BUFSEGMENT>()).ToPointer();

            SegmentPointer->BufferId = IntPtr.Zero;
            SegmentPointer->Offset = offset;
            SegmentPointer->Length = TotalLength;

            _continuationWrapperDelegate = o => ((Action)o)();
        }

        internal void SetBufferId(IntPtr id)
        {
            SegmentPointer->BufferId = id;
        }

        public void DisposeWhenComplete()
        {
            bool taken = false;
            _spinLock.Enter(ref taken);

            disposeOnComplete = !complete;

            _spinLock.Exit();

            if (!disposeOnComplete)
                Dispose();
        }

        public void Dispose()
        {
            _continuation = null;
            complete = false;

            disposeOnComplete = false;

            SegmentPointer->Length = TotalLength;
            _pool.ReleaseBuffer(this);
        }

        public bool IsCompleted
        {
            get
            {
                bool taken = false;
                _spinLock.Enter(ref taken);
                if (complete)
                    _spinLock.Exit();
                return complete;

            }
        }

        public void Set()
        {
            bool taken = false;
            _spinLock.Enter(ref taken);

            complete = true;
            var cont = _continuation;
            _spinLock.Exit();

            cont?.Invoke();

            //if (cont != null)
            //    ThreadPool.QueueUserWorkItem(_continuationWrapperDelegate, cont);

            if (disposeOnComplete)
                Dispose();

        }

        public void OnCompleted(Action continuation)
        {
            _continuation = continuation;
            _spinLock.Exit();
        }

        public RioBufferSegment GetResult() => this;

        public RioBufferSegment GetAwaiter() => this;
    }
}
