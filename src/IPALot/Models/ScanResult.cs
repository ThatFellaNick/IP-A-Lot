// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// ScanResult.cs defines the immutable data returned by scanner services before
// it is shaped for the WinForms grid.
// -----------------------------------------------------------------------------

using System.Net;

namespace IPALot.Models
{
    public sealed class ScanResult
    {
        public ScanResult(IPAddress ipAddress, bool isOnline, string? hostName, string? macAddress, string? vendor, long? roundtripTime, string? notes)
        {
            IpAddress = ipAddress;
            IsOnline = isOnline;
            HostName = hostName;
            MacAddress = macAddress;
            Vendor = vendor;
            RoundtripTime = roundtripTime;
            Notes = notes;
        }

        public IPAddress IpAddress { get; }
        public bool IsOnline { get; }
        public string? HostName { get; }
        public string? MacAddress { get; }
        public string? Vendor { get; }
        public long? RoundtripTime { get; }
        public string? Notes { get; }
    }
}
