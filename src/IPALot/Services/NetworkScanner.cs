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
    private readonly ServiceProbeService _serviceProbeService = new ServiceProbeService();

    public async Task ScanAsync(IReadOnlyList<IPAddress> targets, IProgress<ScanProgress> progress, CancellationToken cancellationToken)
    {
        var completed = 0;
        var targetQueue = new ConcurrentQueue<IPAddress>(targets);
        var workerCount = Math.Min(MaxConcurrency, targets.Count);

        // A bounded worker queue avoids creating one Task per IP, which keeps
        // large ranges from overwhelming older technician machines.
        var scanTasks = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested && targetQueue.TryDequeue(out var target))
            {
                var result = await ScanOneAsync(target, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var currentCompleted = Interlocked.Increment(ref completed);
                progress.Report(new ScanProgress(currentCompleted, targets.Count, result));
            }
        }, cancellationToken)).ToArray();

        await Task.WhenAll(scanTasks);
    }

    private async Task<ScanResult?> ScanOneAsync(IPAddress target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var ping = new Ping();
        PingReply? reply = null;

        try
        {
            // SendPingAsync has its own timeout but no cancellation token on
            // .NET Framework, so race it against a cancellable delay.
            var pingTask = ping.SendPingAsync(target, PingTimeoutMilliseconds);
            var completedTask = await Task.WhenAny(pingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            reply = await pingTask;
        }
        catch (PingException)
        {
            return new ScanResult(
                target,
                ScanStatuses.Unknown,
                hostName: null,
                macAddress: null,
                vendor: null,
                roundtripTime: null,
                notes: "Ping failed",
                detectedServices: null);
        }

        if (reply.Status != IPStatus.Success)
        {
            // Dead hosts stop here. Vendor, ARP, and service checks are only
            // attempted for hosts that answered ICMP.
            return new ScanResult(
                target,
                ScanStatuses.Dead,
                hostName: null,
                macAddress: null,
                vendor: null,
                roundtripTime: null,
                notes: reply.Status.ToString(),
                detectedServices: null);
        }

        // DNS can be slow or missing. Start it in parallel with the local ARP
        // and service checks, then give it a short final window below.
        var hostNameTask = ResolveHostNameAsync(target);
        var macAddress = MacAddressService.GetMacAddress(target);
        var vendor = macAddress is null ? null : _ouiLookupService.LookupVendor(macAddress);
        var detectedServices = await _serviceProbeService.ProbeAsync(target, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var hostName = await WaitForHostNameAsync(hostNameTask, cancellationToken);

        return new ScanResult(
            target,
            ScanStatuses.Alive,
            hostName,
            macAddress,
            vendor,
            reply.RoundtripTime,
            notes: BuildNotes(hostName, macAddress, vendor, detectedServices.Count),
            detectedServices);
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

    private static async Task<string?> WaitForHostNameAsync(Task<string?> hostNameTask, CancellationToken cancellationToken)
    {
        // Do not let a stubborn reverse lookup make the whole scan feel stuck.
        var completedTask = await Task.WhenAny(hostNameTask, Task.Delay(250, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        return completedTask == hostNameTask ? await hostNameTask : null;
    }

    private static string? BuildNotes(string? hostName, string? macAddress, string? vendor, int serviceCount)
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

        if (serviceCount > 0)
        {
            notes.Enqueue($"{serviceCount} thing(s) answered the door");
        }

        return notes.IsEmpty ? null : string.Join("; ", notes);
    }
}
