// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// IpRangeParser.cs accepts friendly IPv4 range formats used by admins in the
// field: CIDR, single IPs, full start-end ranges, and last-octet ranges.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace IPALot.Services;

public static class IpRangeParser
{
    private const int MaxTargetsPerToken = 65_536;

    public static IEnumerable<IPAddress> ParseMany(string input)
    {
        var tokens = SplitAndTrim(input, ',', ';', '\r', '\n');

        foreach (var token in tokens)
        {
            foreach (var address in ParseOne(token))
            {
                yield return address;
            }
        }
    }

    private static IEnumerable<IPAddress> ParseOne(string token)
    {
        if (token.Contains('/'))
        {
            return ParseCidr(token);
        }

        if (token.Contains('-'))
        {
            return ParseHyphenRange(token);
        }

        if (IPAddress.TryParse(token, out var single) && single.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return [single];
        }

        throw new FormatException($"'{token}' is not a valid IPv4 range.");
    }

    private static IEnumerable<IPAddress> ParseCidr(string token)
    {
        var parts = SplitAndTrim(token, '/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var baseAddress) || !int.TryParse(parts[1], out var prefixLength))
        {
            throw new FormatException($"'{token}' is not valid CIDR notation.");
        }

        if (prefixLength is < 0 or > 32)
        {
            throw new FormatException($"'{token}' has an invalid CIDR prefix length.");
        }

        var baseValue = ToUInt32(baseAddress);
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        var network = baseValue & mask;
        var broadcast = network | ~mask;

        var start = prefixLength <= 30 ? network + 1 : network;
        var end = prefixLength <= 30 ? broadcast - 1 : broadcast;
        return BuildRange(start, end, token);
    }

    private static IEnumerable<IPAddress> ParseHyphenRange(string token)
    {
        var parts = SplitAndTrim(token, '-');
        if (parts.Length != 2)
        {
            throw new FormatException($"'{token}' is not a valid range.");
        }

        if (IPAddress.TryParse(parts[0], out var startAddress) && IPAddress.TryParse(parts[1], out var endAddress))
        {
            return BuildRange(ToUInt32(startAddress), ToUInt32(endAddress), token);
        }

        var startOctets = SplitAndTrim(parts[0], '.');
        if (startOctets.Length == 4 && byte.TryParse(startOctets[3], out var startLastOctet) && byte.TryParse(parts[1], out var endLastOctet))
        {
            var start = ToUInt32(IPAddress.Parse($"{startOctets[0]}.{startOctets[1]}.{startOctets[2]}.{startLastOctet}"));
            var end = ToUInt32(IPAddress.Parse($"{startOctets[0]}.{startOctets[1]}.{startOctets[2]}.{endLastOctet}"));
            return BuildRange(start, end, token);
        }

        throw new FormatException($"'{token}' is not a valid IPv4 range.");
    }

    private static IEnumerable<IPAddress> BuildRange(uint start, uint end, string originalToken)
    {
        if (end < start)
        {
            throw new FormatException($"'{originalToken}' ends before it starts.");
        }

        var count = end - start + 1;
        if (count > MaxTargetsPerToken)
        {
            throw new FormatException($"'{originalToken}' expands to {count:N0} addresses. Keep each range at {MaxTargetsPerToken:N0} or fewer.");
        }

        for (var value = start; value <= end; value++)
        {
            yield return FromUInt32(value);
            if (value == uint.MaxValue)
            {
                yield break;
            }
        }
    }

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            throw new FormatException("Only IPv4 addresses are supported.");
        }

        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static IPAddress FromUInt32(uint value)
    {
        return new IPAddress([
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value,
        ]);
    }

    private static string[] SplitAndTrim(string value, params char[] separators)
    {
        return value
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();
    }
}
