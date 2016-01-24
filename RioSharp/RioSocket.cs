using System;
using System.Threading;

namespace RioSharp
{
    public unsafe class RioSocket : RioSocketBase
    {
        public AwaitableQueue2 incommingSegments = new AwaitableQueue2();
        internal RioNativeOverlapped* _overlapped;
        internal IntPtr _adressBuffer;
        private IntPtr _eventHandle;
        private RioTcpSocketPool _pool;

        internal RioSocket(IntPtr overlapped, IntPtr adressBuffer, RioTcpSocketPool pool) :
            base(pool.SendBufferPool, pool.ReciveBufferPool, pool.MaxOutstandingReceive, pool.MaxOutstandingSend, pool.SendCompletionQueue, pool.ReceiveCompletionQueue)
        {
            _overlapped = (RioNativeOverlapped*)overlapped.ToPointer();
            _eventHandle = Imports.CreateEvent(IntPtr.Zero, false, false, null);
            _adressBuffer = adressBuffer;
            _pool = pool;
            unsafe
            {
                var n = (NativeOverlapped*)overlapped.ToPointer();
                n->EventHandle = _eventHandle;
            }
        }


        internal unsafe void ResetOverlapped()
        {
            _overlapped->InternalHigh = IntPtr.Zero;
            _overlapped->InternalLow = IntPtr.Zero;
            _overlapped->OffsetHigh = 0;
            _overlapped->OffsetLow = 0;
            Imports.ResetEvent(_overlapped->EventHandle);
        }

        public override void Dispose()
        {
            incommingSegments.Dispose();
            _pool.Recycle(this);
        }
    }
}

