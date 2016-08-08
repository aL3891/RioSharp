using System;
using System.Threading;

namespace RioSharp
{
    public unsafe class RioConnectionOrientedSocket : RioSocket
    {
        internal RioNativeOverlapped* _overlapped;
        internal IntPtr _adressBuffer;
        private IntPtr _eventHandle;
        private RioConnectionOrientedSocketPool _pool;
        internal long disconnectStartTime;

        internal RioConnectionOrientedSocket(IntPtr overlapped, IntPtr adressBuffer, RioConnectionOrientedSocketPool pool, RioFixedBufferPool sendBufferPool, RioFixedBufferPool receiveBufferPool, RioFixedBufferPool adressBufferPool,
            uint maxOutstandingReceive, uint maxOutstandingSend, IntPtr SendCompletionQueue, IntPtr ReceiveCompletionQueue,
            ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol) :
            base(sendBufferPool, receiveBufferPool, adressBufferPool, maxOutstandingReceive, maxOutstandingSend, SendCompletionQueue, ReceiveCompletionQueue,
                adressFam, sockType, protocol) //ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP
        {
            _overlapped = (RioNativeOverlapped*)overlapped.ToPointer();
            //_eventHandle = Kernel32.CreateEvent(IntPtr.Zero, false, false, null);
            _adressBuffer = adressBuffer;
            _pool = pool;
            //unsafe
            //{
            //    var n = (NativeOverlapped*)overlapped.ToPointer();
            //    n->EventHandle = _eventHandle;
            //}
        }

        internal unsafe void ResetOverlapped()
        {
            _overlapped->InternalHigh = IntPtr.Zero;
            _overlapped->InternalLow = IntPtr.Zero;
            _overlapped->OffsetHigh = 0;
            _overlapped->OffsetLow = 0;
            //Kernel32.ResetEvent(_overlapped->EventHandle);
        }

        public override void Dispose()
        {
            _pool.BeginRecycle(this, false);
        }

        internal void Close()
        {
            WinSock.closesocket(Socket);
            //Kernel32.CloseHandle(_eventHandle);
        }
    }
}

