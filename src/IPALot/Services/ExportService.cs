// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// ExportService.cs writes scan results to common handoff formats without adding
// package dependencies to the small .NET Framework build.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using IPALot.Models;

namespace IPALot.Services;

public static class ExportService
{
    public static void Export(IReadOnlyList<ScanResultRow> rows, string path, ExportFormat format)
    {
        // The caller passes the current visible grid rows, so exports honor the
        // operator's status filters, search text, and expanded service rows.
        switch (format)
        {
            case ExportFormat.Html:
                File.WriteAllText(path, BuildHtml(rows), Encoding.UTF8);
                break;
            case ExportFormat.Csv:
                File.WriteAllText(path, BuildCsv(rows), Encoding.UTF8);
                break;
            case ExportFormat.Json:
                File.WriteAllText(path, BuildJson(rows), Encoding.UTF8);
                break;
            case ExportFormat.Xml:
                File.WriteAllText(path, BuildXml(rows), Encoding.UTF8);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.");
        }
    }

    private static string BuildHtml(IReadOnlyList<ScanResultRow> rows)
    {
        // HTML output is intentionally self-contained for easy ticket/email
        // handoff from technician machines.
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<title>IP A Lot Export</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#101828;background:#f6f7f9}");
        builder.AppendLine("h1{margin-bottom:4px}table{border-collapse:collapse;width:100%;background:#fff}");
        builder.AppendLine("th,td{border:1px solid #d0d5dd;padding:8px;text-align:left;vertical-align:top}");
        builder.AppendLine("th{background:#0b1f3a;color:#fff}tr:nth-child(even){background:#f9fafb}");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("<h1>IP A Lot Export</h1>");
        builder.AppendLine($"<p>{rows.Count:N0} result(s) exported {WebUtility.HtmlEncode(DateTime.Now.ToString("u"))}</p>");
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>Status</th><th>IP</th><th>Name</th><th>MAC</th><th>Vendor</th><th>Ping</th><th>Detected</th><th>Notes</th></tr></thead>");
        builder.AppendLine("<tbody>");

        foreach (var row in rows)
        {
            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{Html(row.Status)}</td>");
            builder.AppendLine($"<td>{Html(row.IpAddress)}</td>");
            builder.AppendLine($"<td>{Html(row.HostName)}</td>");
            builder.AppendLine($"<td>{Html(row.MacAddress)}</td>");
            builder.AppendLine($"<td>{Html(row.Vendor)}</td>");
            builder.AppendLine($"<td>{Html(row.RoundtripTime)}</td>");
            builder.AppendLine($"<td>{Html(DetectedText(row)).Replace(Environment.NewLine, "<br>")}</td>");
            builder.AppendLine($"<td>{Html(row.Notes)}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody>");
        builder.AppendLine("</table>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static string BuildCsv(IReadOnlyList<ScanResultRow> rows)
    {
        // Quote every CSV field. It is simpler than selectively quoting and it
        // handles commas, newlines, and spreadsheet paste behavior cleanly.
        var builder = new StringBuilder();
        builder.AppendLine("Status,IP,Name,MAC,Vendor,Ping,Detected,Notes");

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(row.Status),
                Csv(row.IpAddress),
                Csv(row.HostName),
                Csv(row.MacAddress),
                Csv(row.Vendor),
                Csv(row.RoundtripTime),
                Csv(DetectedText(row)),
                Csv(row.Notes),
            }));
        }

        return builder.ToString();
    }

    private static string BuildJson(IReadOnlyList<ScanResultRow> rows)
    {
        // Manual JSON keeps the executable dependency-light on .NET Framework.
        // Values are still escaped by Json() below before writing.
        var builder = new StringBuilder();
        builder.AppendLine("[");

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            builder.AppendLine("  {");
            builder.AppendLine($"    \"status\": {Json(row.Status)},");
            builder.AppendLine($"    \"ipAddress\": {Json(row.IpAddress)},");
            builder.AppendLine($"    \"hostName\": {Json(row.HostName)},");
            builder.AppendLine($"    \"macAddress\": {Json(row.MacAddress)},");
            builder.AppendLine($"    \"vendor\": {Json(row.Vendor)},");
            builder.AppendLine($"    \"roundtripTime\": {Json(row.RoundtripTime)},");
            builder.AppendLine("    \"detectedServices\": [");

            var services = row.Source?.DetectedServices ?? Array.Empty<DetectedService>();
            for (var serviceIndex = 0; serviceIndex < services.Count; serviceIndex++)
            {
                var service = services[serviceIndex];
                builder.AppendLine("      {");
                builder.AppendLine($"        \"kind\": {Json(service.Kind)},");
                builder.AppendLine($"        \"name\": {Json(service.Name)},");
                builder.AppendLine($"        \"target\": {Json(service.Target)},");
                builder.AppendLine($"        \"notes\": {Json(service.Notes)}");
                builder.Append(serviceIndex == services.Count - 1 ? "      }" : "      },");
                builder.AppendLine();
            }

            builder.AppendLine("    ],");
            builder.AppendLine($"    \"notes\": {Json(row.Notes)}");
            builder.Append(index == rows.Count - 1 ? "  }" : "  },");
            builder.AppendLine();
        }

        builder.AppendLine("]");
        return builder.ToString();
    }

    private static string BuildXml(IReadOnlyList<ScanResultRow> rows)
    {
        // XElement handles XML escaping and keeps this format safer than manual
        // string assembly.
        var document = new XDocument(
            new XElement("ipALotExport",
                new XAttribute("createdUtc", DateTime.UtcNow.ToString("u")),
                rows.Select(row =>
                    new XElement("host",
                        new XAttribute("status", row.Status),
                        new XElement("ipAddress", row.IpAddress),
                        new XElement("hostName", row.HostName),
                        new XElement("macAddress", row.MacAddress),
                        new XElement("vendor", row.Vendor),
                        new XElement("roundtripTime", row.RoundtripTime),
                        new XElement("detectedServices",
                            (row.Source?.DetectedServices ?? Array.Empty<DetectedService>()).Select(service =>
                                new XElement("service",
                                    new XAttribute("kind", service.Kind),
                                    new XElement("name", service.Name),
                                    new XElement("target", service.Target),
                                    new XElement("notes", service.Notes)))),
                        new XElement("notes", row.Notes)))));

        return document.ToString();
    }

    private static string DetectedText(ScanResultRow row)
    {
        return row.Source?.DetectedServices.Count > 0
            ? string.Join(Environment.NewLine, row.Source.DetectedServices.Select(service => service.ToString()))
            : "";
    }

    private static string Csv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string Json(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var builder = new StringBuilder("\"");
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}

public enum ExportFormat
{
    Html,
    Csv,
    Json,
    Xml,
}
