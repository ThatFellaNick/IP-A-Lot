// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// MacAddressService.cs asks the Windows ARP table for a target MAC address after
// a probe has warmed the neighbor cache.
// -----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace IPALot.Services;

public static class MacAddressService
{
    public static string? GetMacAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return null;
        }

        var destination = BitConverter.ToInt32(address.GetAddressBytes(), 0);
        var macBuffer = new byte[6];
        var macLength = macBuffer.Length;

        var result = SendARP(destination, 0, macBuffer, ref macLength);
        if (result != 0 || macLength <= 0)
        {
            return null;
        }

        return string.Join(":", macBuffer.Take(macLength).Select(part => part.ToString("X2")));
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref int physicalAddrLen);
}
