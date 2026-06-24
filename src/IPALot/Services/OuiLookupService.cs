// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// OuiLookupService.cs resolves MAC prefixes to vendor names. A compact built-in
// table covers common hardware, and the full bundled OUI CSV is embedded in the
// executable so the scanner remains portable as a single file.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace IPALot.Services;

public sealed class OuiLookupService
{
    private readonly Dictionary<string, string> _vendors;

    public OuiLookupService()
    {
        _vendors = LoadBuiltInVendors();
        LoadEmbeddedCsv(_vendors);
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
            ["001565"] = "XIAMEN YEALINK NETWORK TECHNOLOGY CO.,LTD",
            ["080027"] = "PCS Systemtechnik / VirtualBox",
            ["0A0027"] = "VirtualBox",
            ["249AD8"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["3497D7"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["3C5A37"] = "Samsung",
            ["3C7C3F"] = "Apple",
            ["40B034"] = "Hewlett Packard",
            ["44DBD2"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["44A842"] = "Dell",
            ["50EBF6"] = "ASUSTek",
            ["5C514F"] = "Intel Corporate",
            ["644F56"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["6C2B59"] = "Dell",
            ["74867A"] = "Dell",
            ["7845C4"] = "Dell",
            ["805E0C"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["805EC0"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["84A93E"] = "Microsoft",
            ["8C1645"] = "Cisco",
            ["A0369F"] = "Intel Corporate",
            ["B061A9"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["B827EB"] = "Raspberry Pi Foundation",
            ["BC2411"] = "Cisco",
            ["C4FC22"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["D4BED9"] = "Dell",
            ["EC1DA9"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["F01653"] = "YEALINK(XIAMEN) NETWORK TECHNOLOGY CO.,LTD.",
            ["F0D5BF"] = "Intel Corporate",
            ["F8CAB8"] = "Dell",
        };
    }

    private static void LoadEmbeddedCsv(Dictionary<string, string> vendors)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("IPALot.Data.oui.csv");
        if (stream is null)
        {
            return;
        }

        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            AddCsvLine(vendors, reader.ReadLine());
        }
    }

    private static void AddCsvLine(Dictionary<string, string> vendors, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var csvLine = line!;
        var parts = csvLine.Split(new[] { ',' }, 2).Select(part => part.Trim()).ToArray();
        if (parts.Length != 2)
        {
            return;
        }

        var prefix = NormalizePrefix(parts[0]);
        if (prefix is not null && !string.IsNullOrWhiteSpace(parts[1]))
        {
            vendors[prefix] = parts[1];
        }
    }

    private static string? NormalizePrefix(string value)
    {
        var hexCharacters = new string(value.Where(Uri.IsHexDigit).Take(6).ToArray()).ToUpperInvariant();
        return hexCharacters.Length == 6 ? hexCharacters : null;
    }
}
