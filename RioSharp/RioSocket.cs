using System;
using System.Threading;

namespace RioSharp
{
    public unsafe class RioSocket : IDisposable
    {
        IntPtr _requestQueue;
        internal IntPtr Socket;
        internal RioFixedBufferPool SendBufferPool, ReceiveBufferPool;
      
        internal RioSocket(RioFixedBufferPool sendBufferPool, RioFixedBufferPool receiveBufferPool,
            uint maxOutstandingReceive, uint maxOutstandingSend, IntPtr SendCompletionQueue, IntPtr ReceiveCompletionQueue,
            ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol)
        {
            if ((Socket = WinSock.WSASocket(adressFam, sockType, protocol, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                WinSock.ThrowLastWSAError();

            SendBufferPool = sendBufferPool;
            ReceiveBufferPool = receiveBufferPool;

            _requestQueue = RioStatic.CreateRequestQueue(Socket, maxOutstandingReceive, 1, maxOutstandingSend, 1, ReceiveCompletionQueue, SendCompletionQueue, GetHashCode());
            WinSock.ThrowLastWSAError();
        }
        
        public RioBufferSegment WritePreAllocated(RioBufferSegment Segment)
        {
            unsafe
            {
                Segment.complete = false;
                if (!RioStatic.Send(_requestQueue, Segment.SegmentPointer, 1, RIO_SEND_FLAGS.DEFER, Segment.Index))
                    WinSock.ThrowLastWSAError();
            }
            return Segment;
        }

        internal unsafe void CommitSend()
        {
            if (!RioStatic.Send(_requestQueue, RIO_BUFSEGMENT.NullSegment, 0, RIO_SEND_FLAGS.COMMIT_ONLY, 0))
                WinSock.ThrowLastWSAError();
        }

        internal unsafe void SendInternal(RioBufferSegment segment, RIO_SEND_FLAGS flags)
        {
            segment.complete = false;
            if (!RioStatic.Send(_requestQueue, segment.SegmentPointer, 1, flags, segment.Index))
                WinSock.ThrowLastWSAError();
        }

        public unsafe RioBufferSegment BeginReceive(RioBufferSegment segment)
        {

            segment.complete = false;
            if (!RioStatic.Receive(_requestQueue, segment.SegmentPointer, 1, RIO_RECEIVE_FLAGS.NONE, segment.Index))
                WinSock.ThrowLastWSAError();

            return segment;
        }

        public unsafe RioBufferSegment WriteFixed(byte[] buffer)
        {
            var currentSegment = SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                Buffer.MemoryCopy(p, currentSegment.RawPointer, currentSegment.TotalLength, buffer.Length);
            }
            currentSegment.SegmentPointer->Length = buffer.Length;
            SendInternal(currentSegment, RIO_SEND_FLAGS.NONE);
            return currentSegment;
        }

        public virtual void Dispose()
        {
            WinSock.closesocket(Socket);
        }


        public int SetSocketOption(IPPROTO_IP_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.setsockopt(Socket, WinSock.IPPROTO_IP, (int)option, (char*)value.ToPointer(), valueLength);
        }

        public int SetSocketOption(IPPROTO_IPV6_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.setsockopt(Socket, WinSock.IPPROTO_IPV6, (int)option, (char*)value.ToPointer(), valueLength);
        }

        public int SetSocketOption(IPPROTO_TCP_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.setsockopt(Socket, WinSock.IPPROTO_TCP, (int)option, (char*)value.ToPointer(), valueLength);
        }

        public int SetSocketOption(IPPROTO_UDP_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.setsockopt(Socket, WinSock.IPPROTO_UDP, (int)option, (char*)value.ToPointer(), valueLength);
        }

        public int SetSocketOption(SOL_SOCKET_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.setsockopt(Socket, WinSock.SOL_SOCKET, (int)option, (char*)value.ToPointer(), valueLength);
        }


        public int GetSocketOption(IPPROTO_IP_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.getsockopt(Socket, WinSock.IPPROTO_IP, (int)option, (char*)value.ToPointer(), &valueLength);
        }

        public int GetSocketOption(IPPROTO_IPV6_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.getsockopt(Socket, WinSock.IPPROTO_IPV6, (int)option, (char*)value.ToPointer(), &valueLength);
        }

        public int GetSocketOption(IPPROTO_TCP_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.getsockopt(Socket, WinSock.IPPROTO_TCP, (int)option, (char*)value.ToPointer(), &valueLength);
        }

        public int GetSocketOption(IPPROTO_UDP_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.getsockopt(Socket, WinSock.IPPROTO_UDP, (int)option, (char*)value.ToPointer(), &valueLength);
        }

        public int GetSocketOption(SOL_SOCKET_SocketOptions option, IntPtr value, int valueLength)
        {
            return WinSock.getsockopt(Socket, WinSock.SOL_SOCKET, (int)option, (char*)value.ToPointer(), &valueLength);
        }
    }
}

