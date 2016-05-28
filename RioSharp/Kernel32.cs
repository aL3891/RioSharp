using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

namespace RioSharp
{
    internal static class Kernel32
    {
        const string Kernel_32 = "Kernel32";
        internal static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport(Kernel_32, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal unsafe static extern IntPtr CreateIoCompletionPort(IntPtr handle, IntPtr hExistingCompletionPort, int puiCompletionKey, uint uiNumberOfConcurrentThreads);

        [DllImport(Kernel_32, SetLastError = true, EntryPoint = "GetQueuedCompletionStatus")]
        [SuppressUnmanagedCodeSecurity]
        internal static extern unsafe bool GetQueuedCompletionStatusRio(IntPtr CompletionPort, out IntPtr lpNumberOfBytes, out IntPtr lpCompletionKey, out RioNativeOverlapped* lpOverlapped, int dwMilliseconds);

        [DllImport(Kernel_32, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern unsafe int GetQueuedCompletionStatus(IntPtr CompletionPort, out IntPtr lpNumberOfBytes, out IntPtr lpCompletionKey, out NativeOverlapped* lpOverlapped, int dwMilliseconds);

        internal static int ThrowLastError()
        {
            var error = Marshal.GetLastWin32Error();

            if (error != 0)
                throw new Win32Exception(error);
            else
                return error;
        }

        [DllImport(Kernel_32, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern IntPtr CreateEvent([In, Optional]IntPtr lpEventAttributes, [In]bool bManualReset, [In]bool bInitialState, [In, Optional]string lpName);

        [DllImport(Kernel_32, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern IntPtr ResetEvent([In]IntPtr handle);

        [DllImport(Kernel_32, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern IntPtr CloseHandle([In]IntPtr handle);


        [DllImport(Kernel_32, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern bool VirtualFree([In]IntPtr lpAddress, [In]uint dwSize, [In]int dwFreeType);

        [DllImport(Kernel_32, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern IntPtr VirtualAlloc([In]IntPtr lpAddress, [In]uint dwSize, [In]int flAllocationType, [In]int flProtect);

    }
}
