using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioConnectionlessSocket : RioSocket
    {
        internal RioConnectionlessSocket(RioSocketPool pool, RioFixedBufferPool sendBufferPool, RioFixedBufferPool receiveBufferPool, RioFixedBufferPool adressBufferPool,
            uint maxOutstandingReceive, uint maxOutstandingSend, IntPtr SendCompletionQueue, IntPtr ReceiveCompletionQueue,
            ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol) :
            base(sendBufferPool, receiveBufferPool, adressBufferPool, maxOutstandingReceive, maxOutstandingSend,
                SendCompletionQueue, ReceiveCompletionQueue,
                adressFam, sockType, protocol) //ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_DGRAM, PROTOCOL.IPPROTO_UDP
        {

        }


        public unsafe void EnableMulticast()
        {
            byte v = 1;
            SetSocketOption(SOL_SOCKET_SocketOptions.SO_BROADCAST, &v, 1);
        }

        //public unsafe void JoinMulticastGroup(IPAddress groupAdress, uint interfaceIndex = 0)
        //{
        //    group_req value = new group_req();
        //    value.gr_interface = interfaceIndex;

        //    var adress = groupAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value.gr_group.padding1, a, (uint)adress.Length);

        //    if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //        value.gr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //    else if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //        value.gr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET6;


        //    var res = SetSocketOption(MCAST_SocketOptions.MCAST_JOIN_GROUP, (void*)&value, Marshal.SizeOf<group_req>());
        //    WinSock.ThrowLastWSAError();

        //}
        
        public unsafe void JoinMulticastGroup(IPAddress groupAdress, uint interfaceIndex = 0)
        {
            var adress = groupAdress.GetAddressBytes();
            if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
            {
                ip_mreq value = new ip_mreq();
                value.imr_interface.s_b4 = (byte)interfaceIndex;
                fixed (byte* a = adress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)adress.Length);

                if (SetSocketOption(IPPROTO_IP_SocketOptions.IP_ADD_MEMBERSHIP, (void*)&value, Marshal.SizeOf<ip_mreq>()) != 0)
                    WinSock.ThrowLastWSAError();
            }
            else if (groupAdress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                ipv6_mreq value = new ipv6_mreq();
                value.ipv6mr_interface = interfaceIndex;
                fixed (byte* a = adress)
                    Unsafe.CopyBlock(value.ipv6mr_multiaddr.Address, a, (uint)adress.Length);

                if (SetSocketOption(IPPROTO_IPV6_SocketOptions.IPV6_ADD_MEMBERSHIP, (void*)&value, Marshal.SizeOf<ipv6_mreq>()) != 0)
                    WinSock.ThrowLastWSAError();
            }
        }

        public unsafe void LeaveMulticastGroup(IPAddress groupAdress, uint interfaceIndex = 0)
        {
            var adress = groupAdress.GetAddressBytes();
            if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
            {
                ip_mreq value = new ip_mreq();
                value.imr_interface.s_b4 = (byte)interfaceIndex;
                fixed (byte* a = adress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)adress.Length);

                if (SetSocketOption(IPPROTO_IP_SocketOptions.IP_DROP_MEMBERSHIP, (void*)&value, Marshal.SizeOf<ip_mreq>()) != 0)
                    WinSock.ThrowLastWSAError();
            }
            else if (groupAdress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                ipv6_mreq value = new ipv6_mreq();
                value.ipv6mr_interface = interfaceIndex;
                fixed (byte* a = adress)
                    Unsafe.CopyBlock(value.ipv6mr_multiaddr.Address, a, (uint)adress.Length);

                if (SetSocketOption(IPPROTO_IPV6_SocketOptions.IPV6_DROP_MEMBERSHIP, (void*)&value, Marshal.SizeOf<ipv6_mreq>()) != 0)
                    WinSock.ThrowLastWSAError();
            }
        }

        public unsafe void JoinMulticastGroup(IPAddress groupAdress, IPAddress sourceAdress, uint interfaceIndex = 0)
        {
            if (groupAdress.AddressFamily != sourceAdress.AddressFamily)
                throw new ArgumentException("Address family must be the same");

            var adress = groupAdress.GetAddressBytes();
            var sourceadress = groupAdress.GetAddressBytes();

            if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
            {
                ip_mreq_source value = new ip_mreq_source();
                value.imr_interface.s_b4 = (byte)interfaceIndex;
                fixed (byte* a = adress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)adress.Length);

                fixed (byte* a = sourceadress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)sourceadress.Length);

                if (SetSocketOption(IPPROTO_IP_SocketOptions.IP_ADD_SOURCE_MEMBERSHIP, (void*)&value, Marshal.SizeOf<ip_mreq_source>()) != 0)
                    WinSock.ThrowLastWSAError();
            }
            else if (groupAdress.AddressFamily == AddressFamily.InterNetworkV6)
            {

                //ipv6_mreq value = new ipv6_mreq();
                //value.ipv6mr_interface = interfaceIndex;
                //fixed (byte* a = adress)
                //    Unsafe.CopyBlock(value.ipv6mr_multiaddr.Address, a, (uint)adress.Length);

                //if (SetSocketOption(IPPROTO_IPV6_SocketOptions., (void*)&value, Marshal.SizeOf<ipv6_mreq>()) != 0)
                //    WinSock.ThrowLastWSAError();
            }
        }

        public unsafe void LeaveMulticastGroup(IPAddress groupAdress, IPAddress sourceAdress, uint interfaceIndex = 0)
        {
            if (groupAdress.AddressFamily != sourceAdress.AddressFamily)
                throw new ArgumentException("Address family must be the same");

            var adress = groupAdress.GetAddressBytes();
            var sourceadress = groupAdress.GetAddressBytes();

            if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
            {
                ip_mreq_source value = new ip_mreq_source();
                value.imr_interface.s_b4 = (byte)interfaceIndex;
                fixed (byte* a = adress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)adress.Length);

                fixed (byte* a = sourceadress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)sourceadress.Length);

                if (SetSocketOption(IPPROTO_IP_SocketOptions.IP_DROP_SOURCE_MEMBERSHIP, (void*)&value, Marshal.SizeOf<ip_mreq_source>()) != 0)
                    WinSock.ThrowLastWSAError();
            }
            else if (groupAdress.AddressFamily == AddressFamily.InterNetworkV6)
            {

                //ipv6_mreq value = new ipv6_mreq();
                //value.ipv6mr_interface = interfaceIndex;
                //fixed (byte* a = adress)
                //    Unsafe.CopyBlock(value.ipv6mr_multiaddr.Address, a, (uint)adress.Length);

                //if (SetSocketOption(IPPROTO_IPV6_SocketOptions., (void*)&value, Marshal.SizeOf<ipv6_mreq>()) != 0)
                //    WinSock.ThrowLastWSAError();
            }
        }


        public unsafe void BlockSource(IPAddress groupAdress, IPAddress sourceAdress, uint interfaceIndex = 0)
        {
            if (groupAdress.AddressFamily != sourceAdress.AddressFamily)
                throw new ArgumentException("Address family must be the same");

            var adress = groupAdress.GetAddressBytes();
            var sourceadress = groupAdress.GetAddressBytes();

            if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
            {
                ip_mreq_source value = new ip_mreq_source();
                value.imr_interface.s_b4 = (byte)interfaceIndex;
                fixed (byte* a = adress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)adress.Length);

                fixed (byte* a = sourceadress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)sourceadress.Length);

                if (SetSocketOption(IPPROTO_IP_SocketOptions.IP_BLOCK_SOURCE, (void*)&value, Marshal.SizeOf<ip_mreq_source>()) != 0)
                    WinSock.ThrowLastWSAError();
            }
            else if (groupAdress.AddressFamily == AddressFamily.InterNetworkV6)
            {

                //ipv6_mreq value = new ipv6_mreq();
                //value.ipv6mr_interface = interfaceIndex;
                //fixed (byte* a = adress)
                //    Unsafe.CopyBlock(value.ipv6mr_multiaddr.Address, a, (uint)adress.Length);

                //if (SetSocketOption(IPPROTO_IPV6_SocketOptions., (void*)&value, Marshal.SizeOf<ipv6_mreq>()) != 0)
                //    WinSock.ThrowLastWSAError();
            }
        }

        public unsafe void UnBlockSource(IPAddress groupAdress, IPAddress sourceAdress, uint interfaceIndex = 0)
        {
            if (groupAdress.AddressFamily != sourceAdress.AddressFamily)
                throw new ArgumentException("Address family must be the same");

            var adress = groupAdress.GetAddressBytes();
            var sourceadress = groupAdress.GetAddressBytes();

            if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
            {
                ip_mreq_source value = new ip_mreq_source();
                value.imr_interface.s_b4 = (byte)interfaceIndex;
                fixed (byte* a = adress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)adress.Length);

                fixed (byte* a = sourceadress)
                    Unsafe.CopyBlock(value.imr_multiaddr.Address, a, (uint)sourceadress.Length);

                if (SetSocketOption(IPPROTO_IP_SocketOptions.IP_BLOCK_SOURCE, (void*)&value, Marshal.SizeOf<ip_mreq_source>()) != 0)
                    WinSock.ThrowLastWSAError();
            }
            else if (groupAdress.AddressFamily == AddressFamily.InterNetworkV6)
            {

                //ipv6_mreq value = new ipv6_mreq();
                //value.ipv6mr_interface = interfaceIndex;
                //fixed (byte* a = adress)
                //    Unsafe.CopyBlock(value.ipv6mr_multiaddr.Address, a, (uint)adress.Length);

                //if (SetSocketOption(IPPROTO_IPV6_SocketOptions., (void*)&value, Marshal.SizeOf<ipv6_mreq>()) != 0)
                //    WinSock.ThrowLastWSAError();
            }
        }

        //public unsafe void JoinMulticastGroup(IPAddress groupAdress, IPAddress sourceAdress, uint interfaceIndex = 0)
        //{
        //    if (groupAdress.AddressFamily != sourceAdress.AddressFamily)
        //        throw new ArgumentException("Address family must be the same");

        //    group_source_req* value = stackalloc group_source_req[1];
        //    value->gsr_interface = interfaceIndex;

        //    var adress = groupAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gsr_group.Address, a, (uint)adress.Length);

        //    adress = sourceAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gsr_source.Address, a, (uint)adress.Length);

        //    if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //    {
        //        value->gsr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //        value->gsr_source.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //    }
        //    else if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //    {
        //        value->gsr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET6;
        //        value->gsr_source.AdressFamily = ADDRESS_FAMILIES.AF_INET6;
        //    }


        //    SetSocketOption(MCAST_SocketOptions.MCAST_JOIN_SOURCE_GROUP, (void*)value, Marshal.SizeOf<group_source_req>());
        //}

        //public unsafe void LeaveMulticastGroup(IPAddress groupAdress, uint interfaceIndex = 0)
        //{
        //    group_req* value = stackalloc group_req[1];
        //    value->gr_interface = interfaceIndex;

        //    var adress = groupAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gr_group.Address, a, (uint)adress.Length);

        //    if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //        value->gr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //    else if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //        value->gr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET6;

        //    SetSocketOption(MCAST_SocketOptions.MCAST_LEAVE_GROUP, (void*)value, Marshal.SizeOf<group_req>());
        //}

        //public unsafe void LeaveMulticastGroup(IPAddress groupAdress, IPAddress sourceAdress, uint interfaceIndex = 0)
        //{
        //    if (groupAdress.AddressFamily != sourceAdress.AddressFamily)
        //        throw new ArgumentException("Address family must be the same");

        //    group_source_req* value = stackalloc group_source_req[1];
        //    value->gsr_interface = interfaceIndex;

        //    var adress = groupAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gsr_group.Address, a, (uint)adress.Length);

        //    adress = sourceAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gsr_source.Address, a, (uint)adress.Length);

        //    if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //    {
        //        value->gsr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //        value->gsr_source.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //    }
        //    else if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //    {
        //        value->gsr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET6;
        //        value->gsr_source.AdressFamily = ADDRESS_FAMILIES.AF_INET6;
        //    }

        //    SetSocketOption(MCAST_SocketOptions.MCAST_LEAVE_SOURCE_GROUP, (void*)value, Marshal.SizeOf<group_source_req>());
        //}

        //public unsafe void BlockSource(IPAddress groupAdress, IPAddress sourceAdress, uint interfaceIndex = 0)
        //{
        //    if (groupAdress.AddressFamily != sourceAdress.AddressFamily)
        //        throw new ArgumentException("Address family must be the same");

        //    group_source_req* value = stackalloc group_source_req[1];
        //    value->gsr_interface = interfaceIndex;

        //    var adress = groupAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gsr_group.Address, a, (uint)adress.Length);

        //    adress = sourceAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gsr_source.Address, a, (uint)adress.Length);

        //    if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //    {
        //        value->gsr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //        value->gsr_source.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //    }
        //    else if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //    {
        //        value->gsr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET6;
        //        value->gsr_source.AdressFamily = ADDRESS_FAMILIES.AF_INET6;
        //    }


        //    SetSocketOption(MCAST_SocketOptions.MCAST_BLOCK_SOURCE, (void*)value, Marshal.SizeOf<group_source_req>());
        //}

        //public unsafe void UnBlockSource(IPAddress groupAdress, IPAddress sourceAdress, uint interfaceIndex = 0)
        //{
        //    if (groupAdress.AddressFamily != sourceAdress.AddressFamily)
        //        throw new ArgumentException("Address family must be the same");

        //    group_source_req* value = stackalloc group_source_req[1];
        //    value->gsr_interface = interfaceIndex;

        //    var adress = groupAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gsr_group.Address, a, (uint)adress.Length);

        //    adress = sourceAdress.GetAddressBytes();
        //    fixed (byte* a = adress)
        //        Unsafe.CopyBlock(value->gsr_source.Address, a, (uint)adress.Length);

        //    if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //    {
        //        value->gsr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //        value->gsr_source.AdressFamily = ADDRESS_FAMILIES.AF_INET;
        //    }
        //    else if (groupAdress.AddressFamily == AddressFamily.InterNetwork)
        //    {
        //        value->gsr_group.AdressFamily = ADDRESS_FAMILIES.AF_INET6;
        //        value->gsr_source.AdressFamily = ADDRESS_FAMILIES.AF_INET6;
        //    }


        //    SetSocketOption(MCAST_SocketOptions.MCAST_UNBLOCK_SOURCE, (void*)value, Marshal.SizeOf<group_source_req>());
        //}
    }
}
