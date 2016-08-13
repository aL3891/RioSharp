using System;
using System.Diagnostics;
using System.Threading;

namespace RioSharp
{
    public unsafe class RioConnectionOrientedSocket : RioSocket
    {
        internal RioNativeOverlapped* _overlapped;
        internal IntPtr _adressBuffer;
        RioConnectionOrientedSocketPool _pool;
        internal long disconnectStartTime;
        Action onreadCompletion;
        Action onSendCompletion;
        internal long lastSendStart;
        internal long lastReceiveStart;
        internal int pendingRecives;
        internal int pendingSends;
        internal long sendTimeout;
        internal long receiveTimeout;


        internal RioConnectionOrientedSocket(IntPtr overlapped, IntPtr adressBuffer, RioConnectionOrientedSocketPool pool, RioFixedBufferPool sendBufferPool, RioFixedBufferPool receiveBufferPool, RioFixedBufferPool adressBufferPool,
                    uint maxOutstandingReceive, uint maxOutstandingSend, IntPtr SendCompletionQueue, IntPtr ReceiveCompletionQueue,
                    ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol) :
                    base(sendBufferPool, receiveBufferPool, adressBufferPool, maxOutstandingReceive, maxOutstandingSend, SendCompletionQueue, ReceiveCompletionQueue,
                        adressFam, sockType, protocol)
        {
            _overlapped = (RioNativeOverlapped*)overlapped.ToPointer();
            _adressBuffer = adressBuffer;
            _pool = pool;
            onreadCompletion = () => { Interlocked.Decrement(ref pendingRecives); };
            onSendCompletion = () => { Interlocked.Decrement(ref pendingSends); };
            sendTimeout = Stopwatch.Frequency * 5;
            receiveTimeout = Stopwatch.Frequency * 5;
        }

        public TimeSpan SendTimeout { get { return TimeSpan.FromSeconds(sendTimeout / Stopwatch.Frequency); } set { sendTimeout = (long)(Stopwatch.Frequency * value.TotalSeconds); } }
        public TimeSpan ReciveTimeout { get { return TimeSpan.FromSeconds(receiveTimeout / Stopwatch.Frequency); } set { receiveTimeout = (long)(Stopwatch.Frequency * value.TotalSeconds); } }

        internal unsafe void ResetOverlapped()
        {
            _overlapped->InternalHigh = IntPtr.Zero;
            _overlapped->InternalLow = IntPtr.Zero;
            _overlapped->OffsetHigh = 0;
            _overlapped->OffsetLow = 0;
        }

        public override void Dispose()
        {
            _pool.BeginRecycle(this, false);
        }

        internal void Close()
        {
            WinSock.closesocket(Socket);
        }

        internal override void Send(RioBufferSegment segment, RioBufferSegment remoteAdress, RIO_SEND_FLAGS flags)
        {
            lastSendStart = RioSocketPool.CurrentTime;
            Interlocked.Increment(ref pendingSends);
            segment._internalCompletionSignal = onSendCompletion;

            base.Send(segment, remoteAdress, flags);
        }

        internal override void Send(RioBufferSegment segment, RIO_SEND_FLAGS flags)
        {
            lastSendStart = RioSocketPool.CurrentTime;
            Interlocked.Increment(ref pendingSends);
            segment._internalCompletionSignal = onSendCompletion;

            base.Send(segment, flags);
        }

        public override RioBufferSegment BeginReceive(RioBufferSegment segment)
        {
            lastReceiveStart = RioSocketPool.CurrentTime;
            Interlocked.Increment(ref pendingRecives);
            segment._internalCompletionSignal = onreadCompletion;

            return base.BeginReceive(segment);
        }
    }
}

