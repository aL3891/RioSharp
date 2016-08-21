using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public abstract class RioSocketPool : IDisposable
    {
        internal RioFixedBufferPool SendBufferPool, ReceiveBufferPool, adressBufferPool;
        IntPtr _sendBufferId, _reciveBufferId, _addrbufferId;
        IntPtr SendCompletionPort, ReceiveCompletionPort;
        protected IntPtr SendCompletionQueue, ReceiveCompletionQueue;
        protected uint MaxOutstandingReceive, MaxOutstandingSend, MaxSockets;

        protected ADDRESS_FAMILIES adressFam;
        protected SOCKET_TYPE sockType;
        protected PROTOCOL protocol;

        internal static long CurrentTime;

        static RioSocketPool()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    CurrentTime = Stopwatch.GetTimestamp();
                    await Task.Delay(500);
                }
            });
        }

        public unsafe RioSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool receivePool, ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxSockets = 256, int adressBufferSize = 1024)
        {
            MaxOutstandingReceive = maxOutstandingReceive;
            MaxOutstandingSend = maxOutstandingSend;
            MaxSockets = maxSockets;
            SendBufferPool = sendPool;
            ReceiveBufferPool = receivePool;
            adressBufferPool = new RioFixedBufferPool(adressBufferSize, Marshal.SizeOf<SOCKADDR_INET>());

            this.adressFam = adressFam;
            this.sockType = sockType;
            this.protocol = protocol;

            var version = new Version(2, 2);
            WSAData data;
            var result = WinSock.WSAStartup((short)version.Raw, out data);
            if (result != 0)
                WinSock.ThrowLastWSAError();

            RioStatic.Initalize();

            if ((ReceiveCompletionPort = Kernel32.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Kernel32.ThrowLastError();

            if ((SendCompletionPort = Kernel32.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Kernel32.ThrowLastError();

            _sendBufferId = RioStatic.RegisterBuffer(SendBufferPool.BufferPointer, (uint)SendBufferPool.TotalLength);
            WinSock.ThrowLastWSAError();
            SendBufferPool.SetBufferId(_sendBufferId);

            _reciveBufferId = RioStatic.RegisterBuffer(ReceiveBufferPool.BufferPointer, (uint)ReceiveBufferPool.TotalLength);
            WinSock.ThrowLastWSAError();
            ReceiveBufferPool.SetBufferId(_reciveBufferId);

            _addrbufferId = RioStatic.RegisterBuffer(adressBufferPool.BufferPointer, (uint)adressBufferPool.TotalLength);
            WinSock.ThrowLastWSAError();
            adressBufferPool.SetBufferId(_addrbufferId);

            var sendCompletionMethod = new RIO_NOTIFICATION_COMPLETION()
            {
                Type = RIO_NOTIFICATION_COMPLETION_TYPE.IOCP_COMPLETION,
                Iocp = new RIO_NOTIFICATION_COMPLETION_IOCP()
                {
                    IocpHandle = SendCompletionPort,
                    QueueCorrelation = 0,
                    Overlapped = (NativeOverlapped*)-1
                }
            };

            if ((SendCompletionQueue = RioStatic.CreateCompletionQueue(MaxOutstandingSend * MaxSockets * 2, sendCompletionMethod)) == IntPtr.Zero)
                WinSock.ThrowLastWSAError();

            var receiveCompletionMethod = new RIO_NOTIFICATION_COMPLETION()
            {
                Type = RIO_NOTIFICATION_COMPLETION_TYPE.IOCP_COMPLETION,
                Iocp = new RIO_NOTIFICATION_COMPLETION_IOCP()
                {
                    IocpHandle = ReceiveCompletionPort,
                    QueueCorrelation = 0,
                    Overlapped = (NativeOverlapped*)-1
                }
            };

            if ((ReceiveCompletionQueue = RioStatic.CreateCompletionQueue(MaxOutstandingReceive * MaxSockets * 2, receiveCompletionMethod)) == IntPtr.Zero)
                WinSock.ThrowLastWSAError();

            Thread receiveThread = new Thread(ProcessReceiveCompletes);
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Thread sendThread = new Thread(ProcessSendCompletes);
            sendThread.IsBackground = true;
            sendThread.Start();
        }

        public unsafe RioBufferSegment PreAllocateWrite(byte[] buffer)
        {
            var currentSegment = SendBufferPool.GetBuffer();
            currentSegment.Write(buffer);
            return currentSegment;
        }

        unsafe void ProcessReceiveCompletes(object o)
        {
            uint maxResults = Math.Min(MaxOutstandingReceive, int.MaxValue);
            RIO_RESULT* results = stackalloc RIO_RESULT[(int)maxResults];
            uint count;
            IntPtr key, bytes;
            NativeOverlapped* overlapped = stackalloc NativeOverlapped[1];
            RIO_RESULT result;
            RioBufferSegment buf;

            while (true)
            {
                RioStatic.Notify(ReceiveCompletionQueue);

                if (Kernel32.GetQueuedCompletionStatus(ReceiveCompletionPort, out bytes, out key, out overlapped, -1) != 0)
                {
                    do
                    {
                        count = RioStatic.DequeueCompletion(ReceiveCompletionQueue, results, maxResults);
                        if (count == 0xFFFFFFFF)
                            WinSock.ThrowLastWSAError();

                        for (var i = 0; i < count; i++)
                        {
                            result = results[i];
                            buf = ReceiveBufferPool.AllSegments[result.RequestCorrelation];
                            buf.SegmentPointer->Length = (int)result.BytesTransferred;
                            buf.Set();
                        }
                    } while (count > 0);
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 0 || error == 735)
                        break;
                    else if (error == 126)
                        continue;
                    else
                        throw new Win32Exception(error);
                }
            }
        }

        unsafe void ProcessSendCompletes(object o)
        {
            uint maxResults = Math.Min(MaxOutstandingSend, int.MaxValue);
            RIO_RESULT* results = stackalloc RIO_RESULT[(int)maxResults];
            RIO_RESULT result;
            RioBufferSegment buf;
            uint count;
            IntPtr key, bytes;
            NativeOverlapped* overlapped = stackalloc NativeOverlapped[1];

            while (true)
            {
                RioStatic.Notify(SendCompletionQueue);
                if (Kernel32.GetQueuedCompletionStatus(SendCompletionPort, out bytes, out key, out overlapped, -1) != 0)
                {
                    do
                    {
                        count = RioStatic.DequeueCompletion(SendCompletionQueue, results, maxResults);
                        if (count == 0xFFFFFFFF)
                            WinSock.ThrowLastWSAError();
                        for (var i = 0; i < count; i++)
                        {
                            result = results[i];
                            buf = SendBufferPool.AllSegments[results[i].RequestCorrelation];
                            buf.Set();
                        }
                    } while (count > 0);
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();


                    if (error == 0 || error == Kernel32.ERROR_ABANDONED_WAIT_0)
                        break;
                    else if (error == 126)
                        continue;
                    else
                        throw new Win32Exception(error);
                }
            }
        }

        public virtual void Dispose()
        {
            RioStatic.DeregisterBuffer(_sendBufferId);
            RioStatic.DeregisterBuffer(_reciveBufferId);

            Kernel32.CloseHandle(SendCompletionPort);
            Kernel32.CloseHandle(ReceiveCompletionPort);
            RioStatic.CloseCompletionQueue(SendCompletionQueue);
            RioStatic.CloseCompletionQueue(ReceiveCompletionQueue);

            WinSock.WSACleanup();

            SendBufferPool.Dispose();
            ReceiveBufferPool.Dispose();
        }
    }
}
