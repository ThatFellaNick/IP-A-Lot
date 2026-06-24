// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// DetectedService.cs describes a network service or shared resource discovered
// while scanning a host.
// -----------------------------------------------------------------------------

namespace IPALot.Models
{
    public sealed class DetectedService
    {
        public DetectedService(string kind, string name, string target, string notes)
        {
            Kind = kind;
            Name = name;
            Target = target;
            Notes = notes;
        }

        public string Kind { get; }
        public string Name { get; }
        public string Target { get; }
        public string Notes { get; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Notes)
                ? $"{Kind}: {Name} ({Target})"
                : $"{Kind}: {Name} ({Target}) - {Notes}";
        }
    }
}
