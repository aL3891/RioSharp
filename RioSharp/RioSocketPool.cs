using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioSocketPool : IDisposable
    {
        internal RioFixedBufferPool SendBufferPool, ReciveBufferPool;
        internal IntPtr _sendBufferId, _reciveBufferId;
        internal IntPtr SendCompletionPort, SendCompletionQueue, ReceiveCompletionPort, ReceiveCompletionQueue;
        internal uint MaxOutstandingReceive, MaxOutstandingSend, MaxConnections, MaxOutsandingCompletions;

        internal ConcurrentDictionary<long, RioSocket> connections = new ConcurrentDictionary<long, RioSocket>();

        public unsafe RioSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxConnections = 1024)
        {
            MaxOutstandingReceive = maxOutstandingReceive;
            MaxOutstandingSend = maxOutstandingSend;
            MaxConnections = maxConnections;
            MaxOutsandingCompletions = (MaxOutstandingReceive + MaxOutstandingSend) * MaxConnections;

            SendBufferPool = sendPool;
            ReciveBufferPool = revicePool;

            var version = new Version(2, 2);
            WSAData data;
            var result = Imports.WSAStartup((short)version.Raw, out data);
            if (result != 0)
                Imports.ThrowLastWSAError();

            RioStatic.Initalize();

            if ((ReceiveCompletionPort = Imports.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();

            if ((SendCompletionPort = Imports.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Imports.ThrowLastError();


            _sendBufferId = RioStatic.RegisterBuffer(SendBufferPool.BufferPointer, SendBufferPool.TotalLength);
            Imports.ThrowLastWSAError();
            SendBufferPool.SetBufferId(_sendBufferId);

            _reciveBufferId = RioStatic.RegisterBuffer(ReciveBufferPool.BufferPointer, ReciveBufferPool.TotalLength);
            Imports.ThrowLastWSAError();
            ReciveBufferPool.SetBufferId(_reciveBufferId);

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

            if ((SendCompletionQueue = RioStatic.CreateCompletionQueue((uint)MaxOutsandingCompletions, sendCompletionMethod)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

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

            if ((ReceiveCompletionQueue = RioStatic.CreateCompletionQueue((uint)MaxOutsandingCompletions, receiveCompletionMethod)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();


            Thread reciveThread = new Thread(ProcessReceiveCompletes);
            reciveThread.IsBackground = true;
            reciveThread.Start();
            Thread sendThread = new Thread(ProcessSendCompletes);
            sendThread.IsBackground = true;
            sendThread.Start();
        }

        public unsafe RioBufferSegment PreAllocateWrite(byte[] buffer)
        {
            var currentSegment = SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                Buffer.MemoryCopy(p, (byte*)currentSegment.Pointer.ToPointer(), SendBufferPool.SegmentLength, buffer.Length);
            }
            currentSegment.ContentLength = (uint)buffer.Length;
            currentSegment.AutoFree = false;
            return currentSegment;
        }

        unsafe void ProcessReceiveCompletes(object o)
        {
            const int maxResults = 1024;
            RIO_RESULT* results = stackalloc RIO_RESULT[maxResults];
            RioSocket connection;
            uint count, key, bytes;
            NativeOverlapped* overlapped;
            RIO_RESULT result;
            RioBufferSegment buf;

            while (true)
            {
                RioStatic.Notify(ReceiveCompletionQueue);
                Imports.ThrowLastWSAError();

                if (Imports.GetQueuedCompletionStatus(ReceiveCompletionPort, out bytes, out key, out overlapped, -1))
                {
                    do
                    {
                        count = RioStatic.DequeueCompletion(ReceiveCompletionQueue, (IntPtr)results, maxResults);
                        Imports.ThrowLastWSAError();

                        for (var i = 0; i < count; i++)
                        {
                            result = results[i];
                            buf = ReciveBufferPool.allSegments[result.RequestCorrelation];
                            if (connections.TryGetValue(result.ConnectionCorrelation, out connection))
                            {
                                buf.ContentLength = result.BytesTransferred;
                                connection.incommingSegments.Enqueue(buf);
                                if (result.BytesTransferred != 0)
                                    connection.ReciveInternal();
                            }
                            else
                                buf.Dispose();
                        }

                    } while (count > 0);
                }
                else
                    Imports.ThrowLastError();
            }
        }

        unsafe void ProcessSendCompletes(object o)
        {
            const int maxResults = 1024;
            RIO_RESULT* results = stackalloc RIO_RESULT[maxResults];
            uint count, key, bytes;
            NativeOverlapped* overlapped;

            while (true)
            {
                RioStatic.Notify(SendCompletionQueue);
                if (Imports.GetQueuedCompletionStatus(SendCompletionPort, out bytes, out key, out overlapped, -1))
                {
                    do
                    {
                        count = RioStatic.DequeueCompletion(SendCompletionQueue, (IntPtr)results, maxResults);
                        Imports.ThrowLastWSAError();
                        for (var i = 0; i < count; i++)
                        {
                            var buf = SendBufferPool.allSegments[results[i].RequestCorrelation];
                            if (buf.AutoFree)
                                buf.Dispose();
                        }

                    } while (count > 0);
                }
                else
                    Imports.ThrowLastError();
            }
        }

        internal void Recycle(RioSocket socket)
        {
            Imports.closesocket(socket._socket);
            Imports.ThrowLastWSAError();

            RioSocket c;
            connections.TryRemove(socket.GetHashCode(), out c);
        }

        public virtual void Dispose()
        {
            RioStatic.DeregisterBuffer(_sendBufferId);
            RioStatic.DeregisterBuffer(_reciveBufferId);

            Imports.WSACleanup();

            SendBufferPool.Dispose();
            ReciveBufferPool.Dispose();
        }

        public unsafe RioSocket Connect(Uri adress)
        {
            IntPtr sock;
            if ((sock = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

            int True = -1;
            UInt32 dwBytes = 0;

            Imports.setsockopt(sock, Imports.IPPROTO_TCP, Imports.TCP_NODELAY, (char*)&True, 4);
            Imports.WSAIoctlGeneral(sock, Imports.SIO_LOOPBACK_FAST_PATH,
                                &True, 4, null, 0,
                                out dwBytes, IntPtr.Zero, IntPtr.Zero);


            var apa = Dns.GetHostAddressesAsync(adress.Host).Result.First(i => i.AddressFamily == AddressFamily.InterNetwork);

            in_addr inAddress = new in_addr();
            inAddress.s_b1 = apa.GetAddressBytes()[0];
            inAddress.s_b2 = apa.GetAddressBytes()[1];
            inAddress.s_b3 = apa.GetAddressBytes()[2];
            inAddress.s_b4 = apa.GetAddressBytes()[3];


            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = Imports.htons((ushort)adress.Port);
            //Imports.ThrowLastWSAError();
            sa.sin_addr = inAddress;

            unsafe
            {
                if (Imports.connect(sock, ref sa, sizeof(sockaddr_in)) == Imports.SOCKET_ERROR)
                    Imports.ThrowLastWSAError();
            }

            var connection = new RioSocket(sock, this);
            connections.TryAdd(connection.GetHashCode(), connection);
            connection.ReciveInternal();
            return connection;
        }

        public RioSocket BindUdpSocket()
        {
            IntPtr sock;
            if ((sock = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_DGRAM, PROTOCOL.IPPROTO_UDP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

            var res = new RioSocket(sock, this);
            res.ReciveInternal();
            return res;
        }
    }
}
