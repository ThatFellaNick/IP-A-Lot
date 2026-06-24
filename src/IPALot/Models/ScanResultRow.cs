// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// ScanResultRow.cs converts scanner data into strings that bind cleanly to the
// desktop grid.
// -----------------------------------------------------------------------------

namespace IPALot.Models
{
    public sealed class ScanResultRow
    {
        public string Status { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string HostName { get; set; } = "";
        public string MacAddress { get; set; } = "";
        public string Vendor { get; set; } = "";
        public string RoundtripTime { get; set; } = "";
        public string Notes { get; set; } = "";
        public ScanResult? Source { get; set; }
        public DetectedService? Service { get; set; }
        public bool IsServiceRow { get; set; }
        public string ParentIpAddress { get; set; } = "";

        public static ScanResultRow FromResult(ScanResult result)
        {
            return new ScanResultRow
            {
                Status = result.Status,
                IpAddress = result.IpAddress.ToString(),
                HostName = result.HostName ?? "",
                MacAddress = result.MacAddress ?? "",
                Vendor = result.Vendor ?? "",
                RoundtripTime = result.RoundtripTime == null ? "" : $"{result.RoundtripTime} ms",
                Notes = result.Notes ?? "",
                Source = result,
            };
        }

        public static ScanResultRow FromService(ScanResultRow parent, DetectedService service)
        {
            return new ScanResultRow
            {
                Status = service.Kind,
                IpAddress = service.Target,
                HostName = service.Name,
                Notes = service.Notes,
                Source = parent.Source,
                Service = service,
                IsServiceRow = true,
                ParentIpAddress = parent.IpAddress,
            };
        }
    }
}
