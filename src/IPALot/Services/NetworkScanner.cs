// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// NetworkScanner.cs performs low-noise host discovery. It uses bounded
// concurrency, ICMP first, and ARP/vendor enrichment only for responsive hosts.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using IPALot.Models;

namespace IPALot.Services;

public sealed class NetworkScanner
{
    private const int MaxConcurrency = 64;
    private const int PingTimeoutMilliseconds = 900;
    private readonly OuiLookupService _ouiLookupService = new OuiLookupService();

    public async Task ScanAsync(IReadOnlyList<IPAddress> targets, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
    {
        var completed = 0;
        using var throttle = new SemaphoreSlim(MaxConcurrency);
        var scanTasks = targets.Select(async target =>
        {
            await throttle.WaitAsync(cancellationToken);

            try
            {
                var result = await ScanOneAsync(target, cancellationToken);
                var currentCompleted = Interlocked.Increment(ref completed);
                progress.Report(new ScanProgress(currentCompleted, targets.Count, result));
            }
            finally
            {
                throttle.Release();
            }
        }).ToArray();

        await Task.WhenAll(scanTasks);
    }

    private async Task<ScanResult?> ScanOneAsync(IPAddress target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var ping = new Ping();
        PingReply? reply = null;

        try
        {
            reply = await ping.SendPingAsync(target, PingTimeoutMilliseconds);
        }
        catch (PingException)
        {
            return null;
        }

        if (reply.Status != IPStatus.Success)
        {
            return null;
        }

        var hostNameTask = ResolveHostNameAsync(target);
        var macAddress = MacAddressService.GetMacAddress(target);
        var vendor = macAddress is null ? null : _ouiLookupService.LookupVendor(macAddress);
        var hostName = await hostNameTask;

        return new ScanResult(
            target,
            isOnline: true,
            hostName,
            macAddress,
            vendor,
            reply.RoundtripTime,
            notes: BuildNotes(hostName, macAddress, vendor));
    }

    private static async Task<string?> ResolveHostNameAsync(IPAddress target)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(target);
            return entry.HostName;
        }
        catch
        {
            return null;
        }
    }

    private static string? BuildNotes(string? hostName, string? macAddress, string? vendor)
    {
        var notes = new ConcurrentQueue<string>();

        if (hostName is null)
        {
            notes.Enqueue("DNS is keeping secrets");
        }

        if (macAddress is null)
        {
            notes.Enqueue("No ARP entry");
        }

        if (macAddress is not null && vendor is null)
        {
            notes.Enqueue("Vendor unknown");
        }

        return notes.IsEmpty ? null : string.Join("; ", notes);
    }
}
