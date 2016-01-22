using System;
using System.Threading;

namespace RioSharp
{
    public unsafe class RioSocket : IDisposable
    {
        internal IntPtr _socket;
        internal RioSocketPool _pool;
        internal IntPtr _requestQueue;
        public AwaitableQueue2 incommingSegments = new AwaitableQueue2();
        internal RioNativeOverlapped* _overlapped;
        internal IntPtr _adressBuffer;
        private IntPtr _eventHandle;

        internal RioSocket(IntPtr overlapped, IntPtr adressBuffer, RioSocketPool pool)
        {
            if ((_socket = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

            _overlapped = (RioNativeOverlapped*)overlapped.ToPointer();
            _eventHandle = Imports.CreateEvent(IntPtr.Zero, false, false, null);

            unsafe
            {
                var n = (NativeOverlapped*)overlapped.ToPointer();
                n->EventHandle = _eventHandle;
                            }

            _adressBuffer = adressBuffer;
            _pool = pool;
            _requestQueue = RioStatic.CreateRequestQueue(_socket, _pool.MaxOutstandingReceive, 1, _pool.MaxOutstandingSend, 1, _pool.ReceiveCompletionQueue, _pool.SendCompletionQueue, GetHashCode());
            Imports.ThrowLastWSAError();
        }

        public void WritePreAllocated(RioBufferSegment Segment)
        {
            unsafe
            {
                if (!RioStatic.Send(_requestQueue, Segment.segmentPointer, 1, RIO_SEND_FLAGS.DEFER, Segment.Index))
                    Imports.ThrowLastWSAError();
            }
        }

        internal unsafe void CommitSend()
        {
            if (!RioStatic.Send(_requestQueue, RIO_BUFSEGMENT.NullSegment, 0, RIO_SEND_FLAGS.COMMIT_ONLY, 0))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void ResetOverlapped()
        {
            _overlapped->InternalHigh = IntPtr.Zero;
            _overlapped->InternalLow = IntPtr.Zero;
            _overlapped->OffsetHigh = 0;
            _overlapped->OffsetLow = 0;
            Imports.ResetEvent(_overlapped->EventHandle);
        }

        internal unsafe void SendInternal(RioBufferSegment segment, RIO_SEND_FLAGS flags)
        {
            if (!RioStatic.Send(_requestQueue, segment.segmentPointer, 1, flags, segment.Index))
                Imports.ThrowLastWSAError();
        }

        internal unsafe void ReciveInternal()
        {
            RioBufferSegment buf;
            if (_pool.ReciveBufferPool.TryGetBuffer(out buf))
            {
                if (!RioStatic.Receive(_requestQueue, buf.segmentPointer, 1, RIO_RECEIVE_FLAGS.NONE, buf.Index))
                    Imports.ThrowLastWSAError();
            }
            else
                ThreadPool.QueueUserWorkItem(o =>
                {
                    var b = _pool.ReciveBufferPool.GetBuffer();
                    if (!RioStatic.Receive(_requestQueue, _pool.ReciveBufferPool.GetBuffer().segmentPointer, 1, RIO_RECEIVE_FLAGS.NONE, b.Index))
                        Imports.ThrowLastWSAError();
                }, null);
        }

        public unsafe void WriteFixed(byte[] buffer)
        {
            var currentSegment = _pool.SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                Buffer.MemoryCopy(p, currentSegment.rawPointer, currentSegment.TotalLength, buffer.Length);
            }
            currentSegment.segmentPointer->Length = buffer.Length;
            SendInternal(currentSegment, RIO_SEND_FLAGS.NONE);
        }

        public virtual void Dispose()
        {
            incommingSegments.Dispose();
            _pool.Recycle(this);
        }
    }
}

