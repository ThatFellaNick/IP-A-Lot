// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// ServiceProbeService.cs performs quick, low-touch checks for common admin
// surfaces such as web interfaces and Windows file shares.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using IPALot.Models;

namespace IPALot.Services;

public sealed class ServiceProbeService
{
    private const int ProbeTimeoutMilliseconds = 450;

    public async Task<IReadOnlyList<DetectedService>> ProbeAsync(IPAddress address, CancellationToken cancellationToken)
    {
        var services = new List<DetectedService>();
        var host = address.ToString();

        // Keep probes intentionally small: a TCP connect tells us whether a
        // common admin surface is present without sending application payloads.
        cancellationToken.ThrowIfCancellationRequested();
        if (await IsTcpOpenAsync(host, 80, cancellationToken))
        {
            services.Add(new DetectedService("HTTP", "Web interface", $"http://{host}/", "Port 80 responded"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (await IsTcpOpenAsync(host, 443, cancellationToken))
        {
            services.Add(new DetectedService("HTTPS", "Secure web interface", $"https://{host}/", "Port 443 responded"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (await IsTcpOpenAsync(host, 445, cancellationToken))
        {
            // Port 445 gets a second pass so visible shares can be expanded and
            // copied directly from the results grid.
            var shares = await GetSharesAsync(host, cancellationToken);
            if (shares.Count == 0)
            {
                services.Add(new DetectedService("Shares", "SMB detected", $@"\\{host}", "Port 445 responded"));
            }
            else
            {
                foreach (var share in shares)
                {
                    services.Add(new DetectedService("Shares", share, $@"\\{host}\{share}", "Share visible"));
                }
            }
        }

        return services;
    }

    private static async Task<bool> IsTcpOpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(host, port);
        var delayTask = Task.Delay(ProbeTimeoutMilliseconds, cancellationToken);
        var completedTask = await Task.WhenAny(connectTask, delayTask);
        cancellationToken.ThrowIfCancellationRequested();

        if (completedTask != connectTask)
        {
            return false;
        }

        try
        {
            await connectTask;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<IReadOnlyList<string>> GetSharesAsync(string host, CancellationToken cancellationToken)
    {
        // NetShareEnum is synchronous. Run it behind a timeout so one slow host
        // cannot stall the entire scan lane.
        var shareTask = Task.Run(() => ShareEnumerationService.GetShares(host), cancellationToken);
        var completedTask = await Task.WhenAny(shareTask, Task.Delay(ProbeTimeoutMilliseconds, cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        return completedTask == shareTask ? await shareTask : Array.Empty<string>();
    }
}

public static class ShareEnumerationService
{
    public static IReadOnlyList<string> GetShares(string host)
    {
        var shares = new List<string>();
        var serverName = $@"\\{host}";
        var resumeHandle = IntPtr.Zero;
        // Level 1 returns the public share name and remark, which is enough for
        // display without requiring deeper filesystem permissions.
        var result = NetShareEnum(serverName, 1, out var buffer, -1, out var entriesRead, out _, ref resumeHandle);

        if (result != 0 || buffer == IntPtr.Zero)
        {
            return shares;
        }

        try
        {
            var offset = buffer;
            var itemSize = Marshal.SizeOf(typeof(ShareInfo1));

            for (var index = 0; index < entriesRead; index++)
            {
                var shareInfo = (ShareInfo1)Marshal.PtrToStructure(offset, typeof(ShareInfo1));
                // Hide administrative shares such as C$ and ADMIN$; they are
                // expected noise for this UI.
                if (!string.IsNullOrWhiteSpace(shareInfo.NetName) && !shareInfo.NetName.EndsWith("$", StringComparison.Ordinal))
                {
                    shares.Add(shareInfo.NetName);
                }

                offset = IntPtr.Add(offset, itemSize);
            }
        }
        finally
        {
            NetApiBufferFree(buffer);
        }

        return shares;
    }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetShareEnum(
        string serverName,
        int level,
        out IntPtr buffer,
        int prefmaxlen,
        out int entriesRead,
        out int totalEntries,
        ref IntPtr resumeHandle);

    [DllImport("Netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShareInfo1
    {
        public string NetName;
        public uint Type;
        public string Remark;
    }
}
