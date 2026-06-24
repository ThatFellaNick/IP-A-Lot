// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// ScanResult.cs defines the immutable data returned by scanner services before
// it is shaped for the WinForms grid.
// -----------------------------------------------------------------------------

using System.Net;
using System.Collections.Generic;
using System.Linq;

namespace IPALot.Models
{
    public sealed class ScanResult
    {
        public ScanResult(IPAddress ipAddress, string status, string? hostName, string? macAddress, string? vendor, long? roundtripTime, string? notes, IEnumerable<DetectedService>? detectedServices)
        {
            IpAddress = ipAddress;
            Status = status;
            HostName = hostName;
            MacAddress = macAddress;
            Vendor = vendor;
            RoundtripTime = roundtripTime;
            Notes = notes;
            DetectedServices = (detectedServices ?? Enumerable.Empty<DetectedService>()).ToList();
        }

        public IPAddress IpAddress { get; }
        public string Status { get; }
        public bool IsOnline => Status == ScanStatuses.Alive;
        public string? HostName { get; }
        public string? MacAddress { get; }
        public string? Vendor { get; }
        public long? RoundtripTime { get; }
        public string? Notes { get; }
        public IReadOnlyList<DetectedService> DetectedServices { get; }
    }

    public static class ScanStatuses
    {
        public const string Alive = "Alive";
        public const string Dead = "Dead";
        public const string Unknown = "Unknown";
    }
}
