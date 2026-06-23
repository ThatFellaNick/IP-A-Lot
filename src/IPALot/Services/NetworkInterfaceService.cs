// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// NetworkInterfaceService.cs detects active local IPv4 networks so the first
// launch starts with useful LAN ranges instead of a blank stare.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IPALot.Services;

public static class NetworkInterfaceService
{
    public static IReadOnlyList<string> GetLocalIpv4Cidrs()
    {
        var ranges = new List<string>();

        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var ipProperties = adapter.GetIPProperties();
            foreach (var unicast in ipProperties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask is null)
                {
                    continue;
                }

                var network = GetNetworkAddress(unicast.Address, unicast.IPv4Mask);
                var prefixLength = CountPrefixBits(unicast.IPv4Mask);
                ranges.Add($"{network}/{prefixLength}");
            }
        }

        return ranges.Distinct().ToList();
    }

    private static IPAddress GetNetworkAddress(IPAddress address, IPAddress subnetMask)
    {
        var addressBytes = address.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();
        var networkBytes = new byte[addressBytes.Length];

        for (var index = 0; index < addressBytes.Length; index++)
        {
            networkBytes[index] = (byte)(addressBytes[index] & maskBytes[index]);
        }

        return new IPAddress(networkBytes);
    }

    private static int CountPrefixBits(IPAddress subnetMask)
    {
        var count = 0;
        foreach (var value in subnetMask.GetAddressBytes())
        {
            var octet = value;
            for (var bit = 0; bit < 8; bit++)
            {
                if ((octet & 0x80) == 0)
                {
                    return count;
                }

                count++;
                octet <<= 1;
            }
        }

        return count;
    }
}
