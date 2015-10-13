using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RIO_RESULT
    {
        public int Status;
        public uint BytesTransferred;
        public long ConnectionCorrelation;
        public long RequestCorrelation;
    }

    public sealed class RIO
    {
        public Imports.RIORegisterBuffer RegisterBuffer;
        public Imports.RIOCreateCompletionQueue CreateCompletionQueue;
        public Imports.RIOCreateRequestQueue CreateRequestQueue;
        public Imports.RIOReceive Receive;
        public Imports.RIOSend Send;
        public Imports.RIONotify Notify;
        public Imports.RIOCloseCompletionQueue CloseCompletionQueue;
        public Imports.RIODequeueCompletion DequeueCompletion;
        public Imports.RIODeregisterBuffer DeregisterBuffer;
        public Imports.RIOResizeCompletionQueue ResizeCompletionQueue;
        public Imports.RIOResizeRequestQueue ResizeRequestQueue;
        public const long CachedValue = long.MinValue;
    }


    public static class RioStatic
    {
        public static Imports.RIORegisterBuffer RegisterBuffer;
        public static Imports.RIOCreateCompletionQueue CreateCompletionQueue;
        public static Imports.RIOCreateRequestQueue CreateRequestQueue;
        public static Imports.RIOReceive Receive;
        public static Imports.RIOSend Send;
        public static Imports.RIONotify Notify;
        public static Imports.RIOCloseCompletionQueue CloseCompletionQueue;
        public static Imports.RIODequeueCompletion DequeueCompletion;
        public static Imports.RIODeregisterBuffer DeregisterBuffer;
        public static Imports.RIOResizeCompletionQueue ResizeCompletionQueue;
        public static Imports.RIOResizeRequestQueue ResizeRequestQueue;

        public unsafe static void Initalize()
        {

            IntPtr tempSocket;
            tempSocket = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.OVERLAPPED);
            Imports.ThrowLastWSAError();

            UInt32 dwBytes = 0;
            var rio = new RIO_EXTENSION_FUNCTION_TABLE();
            Guid RioFunctionsTableId = new Guid("8509e081-96dd-4005-b165-9e2ee8c79e3f");

            if (Imports.WSAIoctl(tempSocket, Imports.SIO_GET_MULTIPLE_EXTENSION_FUNCTION_POINTER,
               ref RioFunctionsTableId, 16, ref rio,
               sizeof(RIO_EXTENSION_FUNCTION_TABLE),
               out dwBytes, IntPtr.Zero, IntPtr.Zero) != 0)
            {
                Imports.ThrowLastWSAError();
            }
            else
            {
                RegisterBuffer = Marshal.GetDelegateForFunctionPointer<Imports.RIORegisterBuffer>(rio.RIORegisterBuffer);
                CreateCompletionQueue = Marshal.GetDelegateForFunctionPointer<Imports.RIOCreateCompletionQueue>(rio.RIOCreateCompletionQueue);
                CreateRequestQueue = Marshal.GetDelegateForFunctionPointer<Imports.RIOCreateRequestQueue>(rio.RIOCreateRequestQueue);
                Notify = Marshal.GetDelegateForFunctionPointer<Imports.RIONotify>(rio.RIONotify);
                DequeueCompletion = Marshal.GetDelegateForFunctionPointer<Imports.RIODequeueCompletion>(rio.RIODequeueCompletion);
                Receive = Marshal.GetDelegateForFunctionPointer<Imports.RIOReceive>(rio.RIOReceive);
                Send = Marshal.GetDelegateForFunctionPointer<Imports.RIOSend>(rio.RIOSend);
                CloseCompletionQueue = Marshal.GetDelegateForFunctionPointer<Imports.RIOCloseCompletionQueue>(rio.RIOCloseCompletionQueue);
                DeregisterBuffer = Marshal.GetDelegateForFunctionPointer<Imports.RIODeregisterBuffer>(rio.RIODeregisterBuffer);
                ResizeCompletionQueue = Marshal.GetDelegateForFunctionPointer<Imports.RIOResizeCompletionQueue>(rio.RIOResizeCompletionQueue);
                ResizeRequestQueue = Marshal.GetDelegateForFunctionPointer<Imports.RIOResizeRequestQueue>(rio.RIOResizeRequestQueue);
            }

            Imports.closesocket(tempSocket);
            Imports.ThrowLastWSAError();
        }
    }

    public struct Version
    {
        public ushort Raw;

        public Version(byte major, byte minor)
        {
            Raw = major;
            Raw <<= 8;
            Raw += minor;
        }

        public byte Major
        {
            get
            {
                ushort result = Raw;
                result >>= 8;
                return (byte)result;
            }
        }

        public byte Minor
        {
            get
            {
                ushort result = Raw;
                result &= 0x00FF;
                return (byte)result;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RIO_EXTENSION_FUNCTION_TABLE
    {
        public UInt32 cbSize;

        public IntPtr RIOReceive;
        public IntPtr RIOReceiveEx;
        public IntPtr RIOSend;
        public IntPtr RIOSendEx;
        public IntPtr RIOCloseCompletionQueue;
        public IntPtr RIOCreateCompletionQueue;
        public IntPtr RIOCreateRequestQueue;
        public IntPtr RIODequeueCompletion;
        public IntPtr RIODeregisterBuffer;
        public IntPtr RIONotify;
        public IntPtr RIORegisterBuffer;
        public IntPtr RIOResizeCompletionQueue;
        public IntPtr RIOResizeRequestQueue;
    }

    public static class Imports
    {
        const string WS2_32 = "WS2_32.dll";

        static Imports()
        {

        }

        const string Kernel_32 = "Kernel32";
        public const long INVALID_HANDLE_VALUE = -1;




        [DllImport(Kernel_32, SetLastError = true)]
        public unsafe static extern IntPtr CreateIoCompletionPort(IntPtr handle, IntPtr hExistingCompletionPort, int puiCompletionKey, uint uiNumberOfConcurrentThreads);

        [DllImport(Kernel_32, SetLastError = true)]
        public static extern unsafe bool GetQueuedCompletionStatus(IntPtr CompletionPort, out uint lpNumberOfBytes, out uint lpCompletionKey, out NativeOverlapped* lpOverlapped, int dwMilliseconds);


        public static int ThrowLastError()
        {
            var error = Marshal.GetLastWin32Error();

            if (error != 0)
                throw new Win32Exception(error);
            else
                return error;
        }

        //readonly static IntPtr RIO_INVALID_BUFFERID = (IntPtr)0xFFFFFFFF;




        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate IntPtr RIORegisterBuffer([In] IntPtr DataBuffer, [In] UInt32 DataLength);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate void RIODeregisterBuffer([In] IntPtr BufferId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public unsafe delegate bool RIOSend([In] IntPtr SocketQueue, [In] RIO_BUFSEGMENT* RioBuffer, [In] UInt32 DataBufferCount, [In] RIO_SEND_FLAGS Flags, [In] long RequestCorrelation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate bool RIOReceive([In] IntPtr SocketQueue, [In] ref RIO_BUFSEGMENT RioBuffer, [In] UInt32 DataBufferCount, [In] RIO_RECEIVE_FLAGS Flags, [In] long RequestCorrelation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate IntPtr RIOCreateCompletionQueue([In] uint QueueSize, [In] RIO_NOTIFICATION_COMPLETION NotificationCompletion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate void RIOCloseCompletionQueue([In] IntPtr CQ);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate IntPtr RIOCreateRequestQueue(
                                      [In] IntPtr Socket,
                                      [In] UInt32 MaxOutstandingReceive,
                                      [In] UInt32 MaxReceiveDataBuffers,
                                      [In] UInt32 MaxOutstandingSend,
                                      [In] UInt32 MaxSendDataBuffers,
                                      [In] IntPtr ReceiveCQ,
                                      [In] IntPtr SendCQ,
                                      [In] long ConnectionCorrelation
                                    );

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate uint RIODequeueCompletion([In] IntPtr CQ, [In] IntPtr ResultArray, [In] uint ResultArrayLength);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate Int32 RIONotify([In] IntPtr CQ);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate bool RIOResizeCompletionQueue([In] IntPtr CQ, [In] UInt32 QueueSize);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate bool RIOResizeRequestQueue([In] IntPtr RQ, [In] UInt32 MaxOutstandingReceive, [In] UInt32 MaxOutstandingSend);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate bool DisconnectEx([In] IntPtr hSocket, [In] IntPtr lpOverlapped, [In] UInt32 dwFlags, [In] UInt32 reserved);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate bool ConnectEx([In] IntPtr s, [In] sockaddr_in name, [In] int namelen, [In] IntPtr lpSendBuffer, [In] uint dwSendDataLength, [Out] uint lpdwBytesSent, [In] IntPtr lpOverlapped);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        public delegate bool AcceptEx([In] IntPtr sListenSocket, [In] IntPtr sAcceptSocket, [In] IntPtr lpOutputBuffer, [In] int dwReceiveDataLength, [In] int dwLocalAddressLength, [In] int dwRemoteAddressLength, [In, Out] ref int lpdwBytesReceived, [In] IntPtr lpOverlapped);



        public const uint IOC_OUT = 0x40000000;
        public const uint IOC_IN = 0x80000000;
        public const uint IOC_INOUT = IOC_IN | IOC_OUT;
        public const uint IOC_WS2 = 0x08000000;
        public const uint IOC_VENDOR = 0x18000000;
        public const uint SIO_GET_MULTIPLE_EXTENSION_FUNCTION_POINTER = IOC_INOUT | IOC_WS2 | 36;
        public const int SIO_LOOPBACK_FAST_PATH = -1744830448;// IOC_IN | IOC_WS2 | 16;
        public const int TCP_NODELAY = 0x0001;
        public const int IPPROTO_TCP = 6;

        public unsafe static RIO Initalize(IntPtr socket)
        {
            UInt32 dwBytes = 0;
            RIO_EXTENSION_FUNCTION_TABLE rio = new RIO_EXTENSION_FUNCTION_TABLE();
            Guid RioFunctionsTableId = new Guid("8509e081-96dd-4005-b165-9e2ee8c79e3f");

            int True = -1;
            var result = setsockopt(socket, IPPROTO_TCP, TCP_NODELAY, (char*)&True, 4);
            if (result != 0)
            {
                var error = Imports.WSAGetLastError();
                Imports.WSACleanup();
                throw new Exception(String.Format("ERROR: setsockopt TCP_NODELAY returned {0}", error));
            }

            result = WSAIoctlGeneral(socket, SIO_LOOPBACK_FAST_PATH,
                                &True, 4, null, 0,
                                out dwBytes, IntPtr.Zero, IntPtr.Zero);

            if (result != 0)
            {
                var error = Imports.WSAGetLastError();
                Imports.WSACleanup();
                throw new Exception(String.Format("ERROR: WSAIoctl SIO_LOOPBACK_FAST_PATH returned {0}", error));
            }

            result = WSAIoctl(socket, SIO_GET_MULTIPLE_EXTENSION_FUNCTION_POINTER,
               ref RioFunctionsTableId, 16, ref rio,
               sizeof(RIO_EXTENSION_FUNCTION_TABLE),
               out dwBytes, IntPtr.Zero, IntPtr.Zero);

            if (result != 0)
            {
                var error = Imports.WSAGetLastError();
                Imports.WSACleanup();
                throw new Exception(String.Format("ERROR: RIOInitalize returned {0}", error));
            }
            else
            {
                RIO rioFunctions = new RIO
                {
                    RegisterBuffer = Marshal.GetDelegateForFunctionPointer<RIORegisterBuffer>(rio.RIORegisterBuffer),
                    CreateCompletionQueue = Marshal.GetDelegateForFunctionPointer<RIOCreateCompletionQueue>(rio.RIOCreateCompletionQueue),
                    CreateRequestQueue = Marshal.GetDelegateForFunctionPointer<RIOCreateRequestQueue>(rio.RIOCreateRequestQueue),
                    Notify = Marshal.GetDelegateForFunctionPointer<RIONotify>(rio.RIONotify),
                    DequeueCompletion = Marshal.GetDelegateForFunctionPointer<RIODequeueCompletion>(rio.RIODequeueCompletion),
                    Receive = Marshal.GetDelegateForFunctionPointer<RIOReceive>(rio.RIOReceive),
                    Send = Marshal.GetDelegateForFunctionPointer<RIOSend>(rio.RIOSend),
                    CloseCompletionQueue = Marshal.GetDelegateForFunctionPointer<RIOCloseCompletionQueue>(rio.RIOCloseCompletionQueue),
                    DeregisterBuffer = Marshal.GetDelegateForFunctionPointer<RIODeregisterBuffer>(rio.RIODeregisterBuffer),
                    ResizeCompletionQueue = Marshal.GetDelegateForFunctionPointer<RIOResizeCompletionQueue>(rio.RIOResizeCompletionQueue),
                    ResizeRequestQueue = Marshal.GetDelegateForFunctionPointer<RIOResizeRequestQueue>(rio.RIOResizeRequestQueue)
                };
                return rioFunctions;
            }
        }




        [DllImport(WS2_32, SetLastError = true)]
        public static extern int WSAIoctl(
          [In] IntPtr socket,
          [In] uint dwIoControlCode,
          [In] ref Guid lpvInBuffer,
          [In] uint cbInBuffer,
          [In, Out] ref RIO_EXTENSION_FUNCTION_TABLE lpvOutBuffer,
          [In] int cbOutBuffer,
          [Out] out uint lpcbBytesReturned,
          [In] IntPtr lpOverlapped,
          [In] IntPtr lpCompletionRoutine
        );

        [DllImport(WS2_32, SetLastError = true)]
        public static extern int connect([In] IntPtr s, [In] ref sockaddr_in name, [In] int namelen);

        [DllImport(WS2_32, SetLastError = true, EntryPoint = "WSAIoctl")]
        public unsafe static extern int WSAIoctlGeneral(
          [In] IntPtr socket,
          [In] int dwIoControlCode,
          [In] int* lpvInBuffer,
          [In] uint cbInBuffer,
          [In] int* lpvOutBuffer,
          [In] int cbOutBuffer,
          [Out] out uint lpcbBytesReturned,
          [In] IntPtr lpOverlapped,
          [In] IntPtr lpCompletionRoutine
        );

        [DllImport(WS2_32, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = true, ThrowOnUnmappableChar = true)]
        internal static extern SocketError WSAStartup([In] short wVersionRequested, [Out] out WSAData lpWSAData);

        [DllImport(WS2_32, SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern IntPtr WSASocket([In] ADDRESS_FAMILIES af, [In] SOCKET_TYPE type, [In] PROTOCOL protocol, [In] IntPtr lpProtocolInfo, [In] Int32 group, [In] SOCKET_FLAGS dwFlags);

        [DllImport(WS2_32, SetLastError = true)]
        public static extern ushort htons([In] ushort hostshort);

        [DllImport(WS2_32, SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern int bind(IntPtr s, ref sockaddr_in name, int namelen);

        [DllImport(WS2_32, SetLastError = true)]
        public static extern int listen(IntPtr s, int backlog);

        [DllImport(WS2_32, SetLastError = true)]
        public unsafe static extern int setsockopt(IntPtr s, int level, int optname, char* optval, int optlen);

        [DllImport(WS2_32, SetLastError = true)]
        public static extern IntPtr accept(IntPtr s, ref sockaddr_in addr, ref int addrlen);

        [DllImport(WS2_32)]
        public static extern Int32 WSAGetLastError();

        public static int ThrowLastWSAError()
        {
            var error = Imports.WSAGetLastError();

            if (error != 0)
                throw new Win32Exception(error);
            else
                return error;
        }


        [DllImport(WS2_32, SetLastError = true)]
        public static extern Int32 WSACleanup();

        [DllImport(WS2_32, SetLastError = true)]
        public static extern int closesocket(IntPtr s);

        public const int SOCKET_ERROR = -1;
        public const int INVALID_SOCKET = -1;
    }

    public enum ADDRESS_FAMILIES : short
    {
        AF_INET = 2,
    }

    public enum SOCKET_TYPE : short
    {
        SOCK_STREAM = 1,
    }

    public enum PROTOCOL : short
    {
        IPPROTO_TCP = 6,
    }

    public enum SOCKET_FLAGS : UInt32
    {
        OVERLAPPED = 0x01,
        MULTIPOINT_C_ROOT = 0x02,
        MULTIPOINT_C_LEAF = 0x04,
        MULTIPOINT_D_ROOT = 0x08,
        MULTIPOINT_D_LEAF = 0x10,
        ACCESS_SYSTEM_SECURITY = 0x40,
        NO_HANDLE_INHERIT = 0x80,
        REGISTERED_IO = 0x100
    }

    public enum RIO_SEND_FLAGS : UInt32
    {
        NONE = 0x00000000,
        DONT_NOTIFY = 0x00000001,
        DEFER = 0x00000002,
        COMMIT_ONLY = 0x00000008
    }
    public enum RIO_RECEIVE_FLAGS : UInt32
    {
        NONE = 0x00000000,
        DONT_NOTIFY = 0x00000001,
        DEFER = 0x00000002,
        WAITALL = 0x00000004,
        COMMIT_ONLY = 0x00000008
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WSAData
    {
        internal short wVersion;
        internal short wHighVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        internal string szDescription;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        internal string szSystemStatus;
        internal short iMaxSockets;
        internal short iMaxUdpDg;
        internal IntPtr lpVendorInfo;
    }


    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct sockaddr_in
    {
        public ADDRESS_FAMILIES sin_family;
        public ushort sin_port;
        public in_addr sin_addr;
        public fixed byte sin_zero[8];
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct in_addr
    {
        [FieldOffset(0)]
        public byte s_b1;
        [FieldOffset(1)]
        public byte s_b2;
        [FieldOffset(2)]
        public byte s_b3;
        [FieldOffset(3)]
        public byte s_b4;
    }



    [StructLayout(LayoutKind.Sequential)]
    public struct RIO_BUFSEGMENT
    {
        internal RIO_BUFSEGMENT(IntPtr bufferId, uint offset, uint length) // should be longs?
        {
            BufferId = bufferId;
            Offset = offset;
            Length = length;
        }

        internal IntPtr BufferId;
        internal readonly uint Offset;
        internal uint Length;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct RIO_NOTIFICATION_COMPLETION
    {
        public RIO_NOTIFICATION_COMPLETION_TYPE Type;
        public RIO_NOTIFICATION_COMPLETION_IOCP Iocp;
    }

    public enum RIO_NOTIFICATION_COMPLETION_TYPE : int
    {
        POLLING = 0,
        EVENT_COMPLETION = 1,
        IOCP_COMPLETION = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RIO_NOTIFICATION_COMPLETION_IOCP
    {
        public IntPtr IocpHandle;
        public ulong QueueCorrelation;
        public NativeOverlapped* Overlapped;
    }
}

