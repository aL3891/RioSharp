using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioTcpClientPool : RioSocketPoolBase
    {
        public unsafe RioTcpClientPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool) : base(sendPool, revicePool)
        {

        }

        public RioTcpConnection Connect(Uri adress)
        {
            IntPtr sock;
            if ((sock = Imports.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.OVERLAPPED)) == IntPtr.Zero)
                Imports.ThrowLastWSAError();

           
            
            in_addr inAddress = new in_addr();
            

            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = Imports.htons((ushort)adress.Port);
            Imports.ThrowLastWSAError();
            sa.sin_addr = inAddress;

            unsafe
            {
                if (Imports.connect(sock, ref sa, sizeof(sockaddr_in)) == Imports.SOCKET_ERROR)
                    Imports.ThrowLastWSAError();
            }

            return new RioTcpConnection(sock, this);
        }
        
        private unsafe static SocketError TryGetAddrInfo(string name, AddressInfoHints flags)
        {
            IntPtr root = IntPtr.Zero;
            string canonicalname = null;
            AddressInfo hints = new AddressInfo();
            hints.ai_flags = flags;
            hints.ai_family = AddressFamily.Unspecified;

            try
            {
                SocketError errorCode = (SocketError)Imports.GetAddrInfoW(name, null, ref hints, out root);
                if (errorCode != SocketError.Success)
                { // Should not throw, return mostly blank hostentry
                    return errorCode;
                }

                AddressInfo* pAddressInfo = *(AddressInfo**)root.ToPointer();

                while (pAddressInfo != IntPtr.Zero.ToPointer())
                {
                    sockaddr_in sockaddr;
                    //
                    // Retrieve the canonical name for the host - only appears in the first AddressInfo
                    // entry in the returned array.
                    //
                    if (canonicalname == null && pAddressInfo->ai_canonname != null)
                    {
                        canonicalname = Marshal.PtrToStringUni((IntPtr)pAddressInfo->ai_canonname);
                    }
                    //
                    // Only process IPv4 or IPv6 Addresses. Note that it's unlikely that we'll
                    // ever get any other address families, but better to be safe than sorry.
                    // We also filter based on whether IPv6 is supported on the current
                    // platform / machine.
                    //
                    if (pAddressInfo->ai_family == AddressFamily.InterNetwork || pAddressInfo->ai_family == AddressFamily.InterNetworkV6)

                    {
                        
                        sockaddr = new sockaddr_in();
                        in_addr inAddress = new in_addr();
                        //
                        // Push address data into the socket address buffer
                        //

                        inAddress.s_b1 = *(pAddressInfo->ai_addr + 0);
                        inAddress.s_b2 = *(pAddressInfo->ai_addr + 1);
                        inAddress.s_b3 = *(pAddressInfo->ai_addr + 2);
                        inAddress.s_b4 = *(pAddressInfo->ai_addr + 3);

                        sockaddr.sin_addr = inAddress;

                    }
                    //
                    // Next addressinfo entry
                    //
                    pAddressInfo = pAddressInfo->ai_next;
                }
            }
            finally
            {
                if (root != IntPtr.Zero)
                {
                    Imports.freeaddrinfo(root);
                }
            }
            
            return SocketError.Success;
        }
    }

    // data structures and types needed for getaddrinfo calls.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal unsafe struct AddressInfo
    {
        internal AddressInfoHints ai_flags;
        internal AddressFamily ai_family;
        internal SocketType ai_socktype;
        internal ProtocolFamily ai_protocol;
        internal int ai_addrlen;
        internal sbyte* ai_canonname;   // Ptr to the cannonical name - check for NULL
        internal byte* ai_addr;         // Ptr to the sockaddr structure
        internal AddressInfo* ai_next;  // Ptr to the next AddressInfo structure
    }

    [Flags]
    internal enum AddressInfoHints
    {
        AI_PASSIVE = 0x01, /* Socket address will be used in bind() call */
        AI_CANONNAME = 0x02, /* Return canonical name in first ai_canonname */
        AI_NUMERICHOST = 0x04, /* Nodename must be a numeric address string */
        AI_FQDN = 0x20000, /* Return the FQDN in ai_canonname. This is different than AI_CANONNAME bit flag that
                                   * returns the canonical name registered in DNS which may be different than the fully
                                   * qualified domain name that the flat name resolved to. Only one of the AI_FQDN and 
                                   * AI_CANONNAME bits can be set.  Win7+ */
    }

    [Flags]
    internal enum NameInfoFlags
    {
        NI_NOFQDN = 0x01, /* Only return nodename portion for local hosts */
        NI_NUMERICHOST = 0x02, /* Return numeric form of the host's address */
        NI_NAMEREQD = 0x04, /* Error if the host's name not in DNS */
        NI_NUMERICSERV = 0x08, /* Return numeric form of the service (port #) */
        NI_DGRAM = 0x10, /* Service is a datagram service */
    }

    public enum ProtocolFamily
    {
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Unknown = AddressFamily.Unknown,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Unspecified = AddressFamily.Unspecified,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Unix = AddressFamily.Unix,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        InterNetwork = AddressFamily.InterNetwork,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        ImpLink = AddressFamily.ImpLink,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Pup = AddressFamily.Pup,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Chaos = AddressFamily.Chaos,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        NS = AddressFamily.NS,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Ipx = AddressFamily.Ipx,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Iso = AddressFamily.Iso,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Osi = AddressFamily.Osi,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Ecma = AddressFamily.Ecma,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        DataKit = AddressFamily.DataKit,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Ccitt = AddressFamily.Ccitt,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Sna = AddressFamily.Sna,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        DecNet = AddressFamily.DecNet,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        DataLink = AddressFamily.DataLink,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Lat = AddressFamily.Lat,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        HyperChannel = AddressFamily.HyperChannel,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        AppleTalk = AddressFamily.AppleTalk,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        NetBios = AddressFamily.NetBios,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        VoiceView = AddressFamily.VoiceView,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        FireFox = AddressFamily.FireFox,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Banyan = AddressFamily.Banyan,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Atm = AddressFamily.Atm,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        InterNetworkV6 = AddressFamily.InterNetworkV6,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Cluster = AddressFamily.Cluster,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Ieee12844 = AddressFamily.Ieee12844,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        Irda = AddressFamily.Irda,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        NetworkDesigners = AddressFamily.NetworkDesigners,
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
     

    }; // enum ProtocolFamily

}
