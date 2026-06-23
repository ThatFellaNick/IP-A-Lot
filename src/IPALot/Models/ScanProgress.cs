// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// ScanProgress.cs carries UI-safe scan progress updates.
// -----------------------------------------------------------------------------

namespace IPALot.Models
{
    public sealed class ScanProgress
    {
        public ScanProgress(int completed, int total, ScanResult? result)
        {
            Completed = completed;
            Total = total;
            Result = result;
        }

        public int Completed { get; }
        public int Total { get; }
        public ScanResult? Result { get; }
    }
}
