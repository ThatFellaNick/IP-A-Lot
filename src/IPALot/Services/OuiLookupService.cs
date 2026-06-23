// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// OuiLookupService.cs resolves MAC prefixes to vendor names. A compact built-in
// table covers common hardware, and an optional oui.csv next to the executable
// can extend coverage without changing the app.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IPALot.Services;

public sealed class OuiLookupService
{
    private readonly Dictionary<string, string> _vendors;

    public OuiLookupService()
    {
        _vendors = LoadBuiltInVendors();
        LoadExternalCsv(_vendors);
    }

    public string? LookupVendor(string macAddress)
    {
        var normalized = NormalizePrefix(macAddress);
        if (normalized is null)
        {
            return null;
        }

        return _vendors.TryGetValue(normalized, out var vendor) ? vendor : null;
    }

    private static Dictionary<string, string> LoadBuiltInVendors()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["000C29"] = "VMware",
            ["00155D"] = "Microsoft",
            ["001C42"] = "Parallels",
            ["005056"] = "VMware",
            ["080027"] = "PCS Systemtechnik / VirtualBox",
            ["0A0027"] = "VirtualBox",
            ["3C5A37"] = "Samsung",
            ["3C7C3F"] = "Apple",
            ["40B034"] = "Hewlett Packard",
            ["44A842"] = "Dell",
            ["50EBF6"] = "ASUSTek",
            ["5C514F"] = "Intel Corporate",
            ["6C2B59"] = "Dell",
            ["74867A"] = "Dell",
            ["7845C4"] = "Dell",
            ["84A93E"] = "Microsoft",
            ["8C1645"] = "Cisco",
            ["A0369F"] = "Intel Corporate",
            ["B827EB"] = "Raspberry Pi Foundation",
            ["BC2411"] = "Cisco",
            ["D4BED9"] = "Dell",
            ["F0D5BF"] = "Intel Corporate",
            ["F8CAB8"] = "Dell",
        };
    }

    private static void LoadExternalCsv(Dictionary<string, string> vendors)
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "oui.csv");
        if (!File.Exists(csvPath))
        {
            return;
        }

        foreach (var line in File.ReadLines(csvPath))
        {
            var parts = line.Split(new[] { ',' }, 2).Select(part => part.Trim()).ToArray();
            if (parts.Length != 2)
            {
                continue;
            }

            var prefix = NormalizePrefix(parts[0]);
            if (prefix is not null && !string.IsNullOrWhiteSpace(parts[1]))
            {
                vendors[prefix] = parts[1];
            }
        }
    }

    private static string? NormalizePrefix(string value)
    {
        var hexCharacters = new string(value.Where(Uri.IsHexDigit).Take(6).ToArray()).ToUpperInvariant();
        return hexCharacters.Length == 6 ? hexCharacters : null;
    }
}
