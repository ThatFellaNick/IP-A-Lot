// -----------------------------------------------------------------------------
// IP A Lot - lightweight network scanner
// Copyright (c) IP A Lot contributors.
//
// MainForm.cs contains the desktop UI, scan coordination, and scan result
// binding. Network probing and parsing are delegated to services so the form
// stays boring in the best possible IT way.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IPALot.Models;
using IPALot.Services;

namespace IPALot;

public sealed class MainForm : Form
{
    private readonly TextBox _rangeInput = new();
    private readonly Button _scanButton = new();
    private readonly Button _stopButton = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progressBar = new();
    private readonly DataGridView _resultsGrid = new();
    private readonly BindingSource _resultsSource = new();
    private readonly List<ScanResultRow> _results = new List<ScanResultRow>();
    private CancellationTokenSource? _scanCancellation;

    public MainForm()
    {
        Text = "IP A Lot";
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 247, 249);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
        Load += (_, _) => PrefillDetectedNetworks();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            AutoSize = true,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "IP A Lot",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(32, 38, 46),
            Margin = new Padding(0, 0, 0, 8),
        };

        var subtitle = new Label
        {
            Text = "Find the devices before they find the ticket queue.",
            AutoSize = true,
            ForeColor = Color.FromArgb(88, 97, 108),
            Margin = new Padding(0, 0, 0, 8),
        };

        var examples = new Label
        {
            Text = "Examples: 192.168.1.0/24, 192.168.2.0-254, 10.0.0.20-10.0.0.50",
            AutoSize = true,
            ForeColor = Color.FromArgb(88, 97, 108),
            Margin = new Padding(0, 0, 0, 8),
        };

        _rangeInput.Dock = DockStyle.Fill;
        _rangeInput.Margin = new Padding(0, 0, 8, 0);

        _scanButton.Text = "Scan";
        _scanButton.AutoSize = true;
        _scanButton.Margin = new Padding(0, 0, 8, 0);
        _scanButton.Click += async (_, _) => await StartScanAsync();

        _stopButton.Text = "Stop";
        _stopButton.AutoSize = true;
        _stopButton.Enabled = false;
        _stopButton.Margin = new Padding(0, 0, 8, 0);
        _stopButton.Click += (_, _) => _scanCancellation?.Cancel();

        var clearButton = new Button
        {
            Text = "Clear",
            AutoSize = true,
        };
        clearButton.Click += (_, _) => ClearResults();

        header.Controls.Add(_rangeInput, 0, 0);
        header.Controls.Add(_scanButton, 1, 0);
        header.Controls.Add(_stopButton, 2, 0);
        header.Controls.Add(clearButton, 3, 0);

        var topStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        };
        topStack.Controls.Add(title);
        topStack.Controls.Add(subtitle);
        topStack.Controls.Add(examples);
        topStack.Controls.Add(header);

        ConfigureResultsGrid();
        _resultsSource.DataSource = _results;
        _resultsGrid.DataSource = _resultsSource;

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));

        _statusLabel.Text = "Ready. The packet shovel is parked.";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.FromArgb(79, 87, 98);
        _statusLabel.Anchor = AnchorStyles.Left;

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Style = ProgressBarStyle.Continuous;

        footer.Controls.Add(_statusLabel, 0, 0);
        footer.Controls.Add(_progressBar, 1, 0);

        root.Controls.Add(topStack, 0, 0);
        root.Controls.Add(_resultsGrid, 0, 1);
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);
    }

    private void ConfigureResultsGrid()
    {
        _resultsGrid.Dock = DockStyle.Fill;
        _resultsGrid.AllowUserToAddRows = false;
        _resultsGrid.AllowUserToDeleteRows = false;
        _resultsGrid.AllowUserToResizeRows = false;
        _resultsGrid.AutoGenerateColumns = false;
        _resultsGrid.BackgroundColor = Color.White;
        _resultsGrid.BorderStyle = BorderStyle.FixedSingle;
        _resultsGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _resultsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _resultsGrid.RowHeadersVisible = false;
        _resultsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _resultsGrid.ReadOnly = true;

        AddGridColumn(nameof(ScanResultRow.Status), "Status", 90);
        AddGridColumn(nameof(ScanResultRow.IpAddress), "IP Address", 140);
        AddGridColumn(nameof(ScanResultRow.HostName), "Host Name", 220);
        AddGridColumn(nameof(ScanResultRow.MacAddress), "MAC", 150);
        AddGridColumn(nameof(ScanResultRow.Vendor), "Vendor", 190);
        AddGridColumn(nameof(ScanResultRow.RoundtripTime), "Ping", 80);
        AddGridColumn(nameof(ScanResultRow.Notes), "Notes", 260, DataGridViewAutoSizeColumnMode.Fill);
    }

    private void AddGridColumn(string propertyName, string headerText, int width, DataGridViewAutoSizeColumnMode mode = DataGridViewAutoSizeColumnMode.None)
    {
        _resultsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = propertyName,
            HeaderText = headerText,
            Width = width,
            AutoSizeMode = mode,
        });
    }

    private void PrefillDetectedNetworks()
    {
        var detectedRanges = NetworkInterfaceService.GetLocalIpv4Cidrs();
        _rangeInput.Text = detectedRanges.Count > 0 ? string.Join(", ", detectedRanges) : "192.168.1.0/24";
    }

    private async Task StartScanAsync()
    {
        if (_scanCancellation is not null)
        {
            return;
        }

        List<IPAddress> targets;
        try
        {
            targets = IpRangeParser.ParseMany(_rangeInput.Text).Distinct().ToList();
        }
        catch (FormatException ex)
        {
            MessageBox.Show(this, ex.Message, "Range needs a tune-up", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Enter at least one IPv4 address or range.", "Nothing to scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ClearResults();
        SetScanningState(true);

        _progressBar.Maximum = targets.Count;
        _statusLabel.Text = $"Scanning {targets.Count:N0} address(es). Coffee may be involved.";

        _scanCancellation = new CancellationTokenSource();
        var progress = new Progress<ScanProgress>(OnScanProgress);

        try
        {
            var scanner = new NetworkScanner();
            await scanner.ScanAsync(targets, progress, _scanCancellation.Token);
            _statusLabel.Text = $"Done. Found {_results.Count(row => row.Status == "Online"):N0} online host(s).";
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Stopped. The packets have been asked to sit quietly.";
        }
        finally
        {
            _scanCancellation.Dispose();
            _scanCancellation = null;
            SetScanningState(false);
        }
    }

    private void OnScanProgress(ScanProgress progress)
    {
        if (progress.Result is not null)
        {
            _results.Add(ScanResultRow.FromResult(progress.Result));
            _resultsSource.ResetBindings(false);
        }

        _progressBar.Value = Math.Min(progress.Completed, _progressBar.Maximum);
        _statusLabel.Text = $"Scanned {progress.Completed:N0} of {progress.Total:N0}.";
    }

    private void SetScanningState(bool scanning)
    {
        _scanButton.Enabled = !scanning;
        _stopButton.Enabled = scanning;
        _rangeInput.Enabled = !scanning;
        _progressBar.Value = 0;
    }

    private void ClearResults()
    {
        _results.Clear();
        _resultsSource.ResetBindings(false);
        _progressBar.Value = 0;
        _statusLabel.Text = "Ready. The packet shovel is parked.";
    }
}
