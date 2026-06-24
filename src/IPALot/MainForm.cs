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
using System.Diagnostics;
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
    private readonly TextBox _searchInput = new();
    private readonly Button _scanButton = new();
    private readonly Button _stopButton = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progressBar = new();
    private readonly DataGridView _resultsGrid = new();
    private readonly TreeView _detailsTree = new();
    private readonly BindingSource _resultsSource = new();
    private readonly System.Windows.Forms.Timer _scanRefreshTimer = new();
    private readonly Image _shareDetectedIcon = BuildShareDetectedIcon();
    private readonly Image _webDetectedIcon = BuildWebDetectedIcon();
    private readonly Image _genericDetectedIcon = SystemIcons.Information.ToBitmap();
    private readonly Image _collapsedIcon = BuildExpanderIcon(false);
    private readonly Image _expandedIcon = BuildExpanderIcon(true);
    private readonly Image _emptyTreeIcon = BuildEmptyTreeIcon();
    private readonly Image _aliveStatusIcon = BuildStatusIcon(Color.FromArgb(34, 197, 94));
    private readonly Image _deadStatusIcon = BuildStatusIcon(Color.FromArgb(148, 163, 184));
    private readonly Image _unknownStatusIcon = BuildStatusIcon(Color.FromArgb(245, 158, 11));
    private readonly List<ScanResultRow> _results = new List<ScanResultRow>();
    private readonly List<ScanResultRow> _visibleResults = new List<ScanResultRow>();
    private readonly HashSet<string> _expandedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool _viewRefreshPending;
    private int _lastCompletedCount;
    private int _lastTotalCount;
    private ToolStripMenuItem _showAliveMenuItem = null!;
    private ToolStripMenuItem _showDeadMenuItem = null!;
    private ToolStripMenuItem _showUnknownMenuItem = null!;
    private ToolStripMenuItem _showDetailsMenuItem = null!;
    private CancellationTokenSource? _scanCancellation;

    public MainForm()
    {
        Text = "IP A Lot";
        Icon = LoadWindowIcon();
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 247, 249);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        BuildLayout();
        ConfigureScanRefreshTimer();
        Load += (_, _) => PrefillDetectedNetworks();
    }

    private static Icon? LoadWindowIcon()
    {
        // WinForms does not always apply the executable icon to the form/taskbar,
        // so the main window explicitly reuses the icon embedded in the app.
        return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
    }

    private void BuildLayout()
    {
        MainMenuStrip = BuildMenu();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 5,
            AutoSize = true,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
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

        _searchInput.Dock = DockStyle.Fill;
        _searchInput.Margin = new Padding(0, 0, 8, 0);
        _searchInput.TextChanged += (_, _) => ApplyView();

        _scanButton.Text = "Scan";
        _scanButton.AutoSize = true;
        _scanButton.Margin = new Padding(0, 0, 8, 0);
        _scanButton.Click += async (_, _) => await StartScanAsync();

        _stopButton.Text = "Stop";
        _stopButton.AutoSize = true;
        _stopButton.Enabled = false;
        _stopButton.Margin = new Padding(0, 0, 8, 0);
        _stopButton.Click += (_, _) => StopScan();

        var clearButton = new Button
        {
            Text = "Clear",
            AutoSize = true,
        };
        clearButton.Click += (_, _) => ClearResults();

        header.Controls.Add(_rangeInput, 0, 0);
        header.Controls.Add(_searchInput, 1, 0);
        header.Controls.Add(_scanButton, 2, 0);
        header.Controls.Add(_stopButton, 3, 0);
        header.Controls.Add(clearButton, 4, 0);

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
        ConfigureDetailsTree();
        _resultsSource.DataSource = _visibleResults;
        _resultsGrid.DataSource = _resultsSource;

        var body = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 330,
            Panel2MinSize = 120,
        };
        body.Panel1.Controls.Add(_resultsGrid);
        body.Panel2.Controls.Add(_detailsTree);

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

        root.Controls.Add(MainMenuStrip, 0, 0);
        root.Controls.Add(topStack, 0, 1);
        root.Controls.Add(body, 0, 2);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
    }

    private MenuStrip BuildMenu()
    {
        _showAliveMenuItem = new ToolStripMenuItem("Show alive") { Checked = true, CheckOnClick = true };
        _showDeadMenuItem = new ToolStripMenuItem("Show dead") { Checked = false, CheckOnClick = true };
        _showUnknownMenuItem = new ToolStripMenuItem("Show unknown") { Checked = true, CheckOnClick = true };
        _showDetailsMenuItem = new ToolStripMenuItem("Show details pane") { Checked = true, CheckOnClick = true };

        _showAliveMenuItem.CheckedChanged += (_, _) => ApplyView();
        _showDeadMenuItem.CheckedChanged += (_, _) => ApplyView();
        _showUnknownMenuItem.CheckedChanged += (_, _) => ApplyView();
        _showDetailsMenuItem.CheckedChanged += (_, _) => _detailsTree.Parent!.Visible = _showDetailsMenuItem.Checked;

        var exportMenu = new ToolStripMenuItem("Export");
        exportMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("HTML...", null, (_, _) => ExportVisibleResults(ExportFormat.Html)),
            new ToolStripMenuItem("CSV...", null, (_, _) => ExportVisibleResults(ExportFormat.Csv)),
            new ToolStripMenuItem("JSON...", null, (_, _) => ExportVisibleResults(ExportFormat.Json)),
            new ToolStripMenuItem("XML...", null, (_, _) => ExportVisibleResults(ExportFormat.Xml)),
        });

        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            exportMenu,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Quit", null, (_, _) => Close()),
        });

        var expandAll = new ToolStripMenuItem("Expand all", null, (_, _) => _detailsTree.ExpandAll());
        var collapseAll = new ToolStripMenuItem("Collapse all", null, (_, _) => _detailsTree.CollapseAll());
        var viewMenu = new ToolStripMenuItem("View");
        viewMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            expandAll,
            collapseAll,
            new ToolStripSeparator(),
            _showAliveMenuItem,
            _showDeadMenuItem,
            _showUnknownMenuItem,
            new ToolStripSeparator(),
            _showDetailsMenuItem,
        });

        return new MenuStrip
        {
            Dock = DockStyle.Top,
            Items = { fileMenu, viewMenu },
        };
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

        _resultsGrid.Columns.Add(new DataGridViewImageColumn
        {
            Name = "TreeIcon",
            HeaderText = "",
            Width = 34,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
        });
        _resultsGrid.Columns.Add(new DataGridViewImageColumn
        {
            Name = "StatusIcon",
            HeaderText = "",
            Width = 34,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            ToolTipText = "Status",
        });
        AddGridColumn(nameof(ScanResultRow.IpAddress), "IP Address", 140);
        AddGridColumn(nameof(ScanResultRow.HostName), "Host Name", 220);
        AddGridColumn(nameof(ScanResultRow.MacAddress), "MAC", 150);
        AddGridColumn(nameof(ScanResultRow.Vendor), "Vendor", 190);
        AddGridColumn(nameof(ScanResultRow.RoundtripTime), "Ping", 80);
        AddGridColumn(nameof(ScanResultRow.Notes), "Notes", 260, DataGridViewAutoSizeColumnMode.Fill);

        _resultsGrid.CellContentClick += ResultsGrid_CellContentClick;
        _resultsGrid.CellFormatting += ResultsGrid_CellFormatting;
        _resultsGrid.CellToolTipTextNeeded += ResultsGrid_CellToolTipTextNeeded;
        _resultsGrid.CellMouseDown += ResultsGrid_CellMouseDown;
        _resultsGrid.SelectionChanged += (_, _) => UpdateDetailsPane(GetSelectedRow());
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
        _searchInput.Text = "";
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
        _lastCompletedCount = 0;
        _lastTotalCount = targets.Count;
        _viewRefreshPending = false;
        _statusLabel.Text = $"Scanning {targets.Count:N0} address(es). Coffee may be involved.";

        _scanCancellation = new CancellationTokenSource();
        var progress = new Progress<ScanProgress>(OnScanProgress);

        try
        {
            var scanner = new NetworkScanner();
            await scanner.ScanAsync(targets, progress, _scanCancellation.Token);
            FlushScanProgress();
            _statusLabel.Text = $"Done. Found {_results.Count(row => row.Status == ScanStatuses.Alive):N0} alive host(s).";
        }
        catch (OperationCanceledException)
        {
            FlushScanProgress();
            _statusLabel.Text = "Stopped. The packets have been asked to sit quietly.";
        }
        finally
        {
            _scanRefreshTimer.Stop();
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
            _viewRefreshPending = true;
        }

        _lastCompletedCount = progress.Completed;
        _lastTotalCount = progress.Total;
    }

    private void SetScanningState(bool scanning)
    {
        _scanButton.Enabled = !scanning;
        _stopButton.Enabled = scanning;
        _stopButton.Text = "Stop";
        _rangeInput.Enabled = !scanning;
        _progressBar.Value = 0;

        if (scanning)
        {
            _scanRefreshTimer.Start();
        }
        else
        {
            _scanRefreshTimer.Stop();
        }
    }

    private void StopScan()
    {
        if (_scanCancellation is null || _scanCancellation.IsCancellationRequested)
        {
            return;
        }

        _statusLabel.Text = "Stopping scan...";
        _stopButton.Enabled = false;
        _stopButton.Text = "Stopping";
        _scanCancellation.Cancel();
    }

    private void ClearResults()
    {
        _results.Clear();
        _visibleResults.Clear();
        _expandedHosts.Clear();
        _lastCompletedCount = 0;
        _lastTotalCount = 0;
        _viewRefreshPending = false;
        _resultsSource.ResetBindings(false);
        _detailsTree.Nodes.Clear();
        _progressBar.Value = 0;
        _statusLabel.Text = "Ready. The packet shovel is parked.";
    }

    private void ConfigureScanRefreshTimer()
    {
        _scanRefreshTimer.Interval = 150;
        _scanRefreshTimer.Tick += (_, _) => FlushScanProgress();
    }

    private void FlushScanProgress()
    {
        if (_lastTotalCount > 0)
        {
            _progressBar.Value = Math.Min(_lastCompletedCount, _progressBar.Maximum);
            if (_scanCancellation is null || !_scanCancellation.IsCancellationRequested)
            {
                _statusLabel.Text = $"Scanned {_lastCompletedCount:N0} of {_lastTotalCount:N0}.";
            }
        }

        if (_viewRefreshPending)
        {
            _viewRefreshPending = false;
            ApplyView();
        }
    }

    private void ConfigureDetailsTree()
    {
        _detailsTree.Dock = DockStyle.Fill;
        _detailsTree.HideSelection = false;
        _detailsTree.Nodes.Add("Select a result to inspect detected things.");
    }

    private void ApplyView()
    {
        var search = _searchInput.Text.Trim();
        var filtered = _results.Where(PassesStatusFilter).Where(row => MatchesSearch(row, search)).ToList();

        _visibleResults.Clear();
        foreach (var row in filtered)
        {
            _visibleResults.Add(row);
            if (_expandedHosts.Contains(row.IpAddress) && row.Source?.DetectedServices.Count > 0)
            {
                _visibleResults.AddRange(row.Source.DetectedServices.Select(service => ScanResultRow.FromService(row, service)));
            }
        }

        _resultsSource.ResetBindings(false);
        UpdateDetailsPane(GetSelectedRow());
    }

    private bool PassesStatusFilter(ScanResultRow row)
    {
        if (row.Status == ScanStatuses.Alive)
        {
            return _showAliveMenuItem.Checked;
        }

        if (row.Status == ScanStatuses.Dead)
        {
            return _showDeadMenuItem.Checked;
        }

        return _showUnknownMenuItem.Checked;
    }

    private static bool MatchesSearch(ScanResultRow row, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Contains(row.IpAddress, search)
            || Contains(row.HostName, search)
            || Contains(row.MacAddress, search)
            || Contains(row.Vendor, search)
            || Contains(row.Notes, search)
            || row.Source?.DetectedServices.Any(service => Contains(service.ToString(), search)) == true;
    }

    private static bool Contains(string value, string search)
    {
        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private ScanResultRow? GetSelectedRow()
    {
        return _resultsGrid.CurrentRow?.DataBoundItem as ScanResultRow;
    }

    private void UpdateDetailsPane(ScanResultRow? row)
    {
        _detailsTree.Nodes.Clear();
        if (row?.Service is not null)
        {
            var serviceNode = _detailsTree.Nodes.Add(row.Service.ToString());
            serviceNode.Nodes.Add($"Type: {row.Service.Kind}");
            serviceNode.Nodes.Add($"Path: {row.Service.Target}");
            AddIfPresent(serviceNode, "Notes", row.Service.Notes);
            serviceNode.Expand();
            return;
        }

        if (row?.Source is null)
        {
            _detailsTree.Nodes.Add("Select a result to inspect detected things.");
            return;
        }

        var hostLabel = string.IsNullOrWhiteSpace(row.HostName) ? row.IpAddress : $"{row.HostName} ({row.IpAddress})";
        var hostNode = _detailsTree.Nodes.Add(hostLabel);
        hostNode.Nodes.Add($"Status: {row.Status}");
        AddIfPresent(hostNode, "MAC", row.MacAddress);
        AddIfPresent(hostNode, "Vendor", row.Vendor);

        var detectedNode = hostNode.Nodes.Add("Detected");
        if (row.Source.DetectedServices.Count == 0)
        {
            detectedNode.Nodes.Add("Nothing extra detected");
        }
        else
        {
            foreach (var service in row.Source.DetectedServices)
            {
                var serviceNode = detectedNode.Nodes.Add(service.ToString());
                serviceNode.Tag = service.Target;
            }
        }

        hostNode.Expand();
        detectedNode.Expand();
    }

    private static void AddIfPresent(TreeNode parent, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parent.Nodes.Add($"{label}: {value}");
        }
    }

    private void ResultsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _resultsGrid.Columns[e.ColumnIndex].Name != "TreeIcon")
        {
            return;
        }

        var row = _resultsGrid.Rows[e.RowIndex].DataBoundItem as ScanResultRow;
        ToggleHostExpansion(row);
    }

    private void ResultsGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        if (_resultsGrid.Columns[e.ColumnIndex].Name == "StatusIcon")
        {
            var statusRow = _resultsGrid.Rows[e.RowIndex].DataBoundItem as ScanResultRow;
            e.Value = GetStatusIcon(statusRow);
            e.FormattingApplied = true;
            return;
        }

        if (_resultsGrid.Columns[e.ColumnIndex].Name != "TreeIcon")
        {
            return;
        }

        var row = _resultsGrid.Rows[e.RowIndex].DataBoundItem as ScanResultRow;
        e.Value = GetTreeIcon(row);
        e.FormattingApplied = true;
    }

    private void ResultsGrid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        var row = _resultsGrid.Rows[e.RowIndex].DataBoundItem as ScanResultRow;
        if (_resultsGrid.Columns[e.ColumnIndex].Name == "StatusIcon")
        {
            e.ToolTipText = row?.Status ?? "";
            return;
        }

        if (_resultsGrid.Columns[e.ColumnIndex].Name == "TreeIcon")
        {
            e.ToolTipText = BuildTreeTooltip(row);
        }
    }

    private void ResultsGrid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0)
        {
            return;
        }

        _resultsGrid.ClearSelection();
        _resultsGrid.Rows[e.RowIndex].Selected = true;
        _resultsGrid.CurrentCell = _resultsGrid.Rows[e.RowIndex].Cells[Math.Max(e.ColumnIndex, 0)];

        var row = _resultsGrid.Rows[e.RowIndex].DataBoundItem as ScanResultRow;
        if (row?.Service is not null)
        {
            BuildDetectedServiceActionMenu(row.Service).Show(Cursor.Position);
        }
        else if (row is not null)
        {
            BuildCopyMenu(row).Show(Cursor.Position);
        }
    }

    private void ToggleHostExpansion(ScanResultRow? row)
    {
        if (row?.IsServiceRow != false || row.Source?.DetectedServices.Count == 0)
        {
            return;
        }

        if (!_expandedHosts.Add(row.IpAddress))
        {
            _expandedHosts.Remove(row.IpAddress);
        }

        ApplyView();
    }

    private ContextMenuStrip BuildDetectedServiceActionMenu(DetectedService service)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Explore", GetServiceIcon(service.Kind), (_, _) => ExploreDetectedService(service));
        menu.Items.Add("Copy path", null, (_, _) => CopyText(service.Target));
        menu.Items.Add("Properties", null, (_, _) => ShowDetectedServiceProperties(service));
        return menu;
    }

    private static string BuildTreeTooltip(ScanResultRow? row)
    {
        if (row?.Service is not null)
        {
            return row.Service.ToString();
        }

        if (row?.Source?.DetectedServices.Count > 0)
        {
            return "Expand detected items";
        }

        return "Nothing extra detected";
    }

    private void ExploreDetectedService(DetectedService service)
    {
        try
        {
            Process.Start(service.Target);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open detected item", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowDetectedServiceProperties(DetectedService service)
    {
        var details = string.Join(Environment.NewLine, new[]
        {
            $"Type: {service.Kind}",
            $"Name: {service.Name}",
            $"Path: {service.Target}",
            $"Notes: {service.Notes}",
        });

        MessageBox.Show(this, details, "Detected item properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private Image GetTreeIcon(ScanResultRow? row)
    {
        if (row?.Service is not null)
        {
            return _emptyTreeIcon;
        }

        if (row?.Source?.DetectedServices.Count > 0)
        {
            return _expandedHosts.Contains(row.IpAddress) ? _expandedIcon : _collapsedIcon;
        }

        return _emptyTreeIcon;
    }

    private Image GetStatusIcon(ScanResultRow? row)
    {
        if (row?.IsServiceRow == true)
        {
            return GetServiceIcon(row.Service?.Kind ?? "");
        }

        return row?.Status switch
        {
            ScanStatuses.Alive => _aliveStatusIcon,
            ScanStatuses.Dead => _deadStatusIcon,
            _ => _unknownStatusIcon,
        };
    }

    private Image GetServiceIcon(string kind)
    {
        return GetServiceIconKind(kind) switch
        {
            "shares" => _shareDetectedIcon,
            "web" => _webDetectedIcon,
            _ => _genericDetectedIcon,
        };
    }

    private static string GetServiceIconKind(string kind)
    {
        if (kind.Equals("Shares", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("shares", StringComparison.OrdinalIgnoreCase))
        {
            return "shares";
        }

        if (kind.Equals("HTTP", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("HTTPS", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("web", StringComparison.OrdinalIgnoreCase))
        {
            return "web";
        }

        return "generic";
    }

    private static Image BuildEmptyTreeIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.Transparent);
        return bitmap;
    }

    private static Image BuildExpanderIcon(bool expanded)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.FromArgb(55, 65, 81));

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var points = expanded
            ? new[] { new Point(4, 6), new Point(12, 6), new Point(8, 11) }
            : new[] { new Point(6, 4), new Point(11, 8), new Point(6, 12) };

        graphics.FillPolygon(brush, points);
        return bitmap;
    }

    private static Image BuildStatusIcon(Color color)
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var fillBrush = new SolidBrush(color);
        using var borderPen = new Pen(Color.FromArgb(75, 85, 99));

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        graphics.FillEllipse(fillBrush, 3, 3, 10, 10);
        graphics.DrawEllipse(borderPen, 3, 3, 10, 10);
        return bitmap;
    }

    private static Image BuildShareDetectedIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var borderPen = new Pen(Color.FromArgb(136, 96, 18));
        using var tabBrush = new SolidBrush(Color.FromArgb(249, 206, 97));
        using var bodyBrush = new SolidBrush(Color.FromArgb(255, 225, 128));

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        graphics.FillRectangle(tabBrush, 2, 4, 5, 3);
        graphics.FillRectangle(bodyBrush, 2, 6, 12, 8);
        graphics.DrawRectangle(borderPen, 2, 5, 12, 9);
        graphics.DrawLine(borderPen, 2, 6, 14, 6);
        return bitmap;
    }

    private static Image BuildWebDetectedIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        using var borderPen = new Pen(Color.FromArgb(36, 100, 168));
        using var linePen = new Pen(Color.FromArgb(89, 151, 214));
        using var fillBrush = new SolidBrush(Color.FromArgb(218, 238, 255));

        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        graphics.FillEllipse(fillBrush, 2, 2, 12, 12);
        graphics.DrawEllipse(borderPen, 2, 2, 12, 12);
        graphics.DrawLine(linePen, 8, 2, 8, 14);
        graphics.DrawArc(linePen, 4, 2, 8, 12, 90, 180);
        graphics.DrawArc(linePen, 4, 2, 8, 12, -90, 180);
        graphics.DrawLine(linePen, 3, 8, 13, 8);
        return bitmap;
    }

    private static ContextMenuStrip BuildCopyMenu(ScanResultRow row)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Copy IP", null, (_, _) => CopyText(row.IpAddress));
        menu.Items.Add("Copy MAC", null, (_, _) => CopyText(row.MacAddress));
        menu.Items.Add("Copy Name", null, (_, _) => CopyText(row.HostName));
        menu.Items.Add("Copy All", null, (_, _) => CopyText(BuildCopyAllText(row)));
        return menu;
    }

    private static void CopyText(string value)
    {
        Clipboard.SetText(string.IsNullOrWhiteSpace(value) ? " " : value);
    }

    private static string BuildCopyAllText(ScanResultRow row)
    {
        var detected = row.Source?.DetectedServices.Count > 0
            ? string.Join(Environment.NewLine, row.Source.DetectedServices.Select(service => service.ToString()))
            : "";

        return string.Join(Environment.NewLine, new[]
        {
            $"Status: {row.Status}",
            $"IP: {row.IpAddress}",
            $"Name: {row.HostName}",
            $"MAC: {row.MacAddress}",
            $"Vendor: {row.Vendor}",
            $"Ping: {row.RoundtripTime}",
            $"Detected: {detected}",
            $"Notes: {row.Notes}",
        });
    }

    private void ExportVisibleResults(ExportFormat format)
    {
        if (_visibleResults.Count == 0)
        {
            MessageBox.Show(this, "There are no visible results to export.", "Export needs results", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var saveDialog = new SaveFileDialog
        {
            AddExtension = true,
            FileName = $"ip-a-lot-{DateTime.Now:yyyyMMdd-HHmmss}.{GetExportExtension(format)}",
            Filter = GetExportFilter(format),
            OverwritePrompt = true,
            Title = "Export visible results",
        };

        if (saveDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ExportService.Export(_visibleResults, saveDialog.FileName, format);
            _statusLabel.Text = $"Exported {_visibleResults.Count:N0} result(s) to {saveDialog.FileName}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export did not finish", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetExportExtension(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Html => "html",
            ExportFormat.Csv => "csv",
            ExportFormat.Json => "json",
            ExportFormat.Xml => "xml",
            _ => "txt",
        };
    }

    private static string GetExportFilter(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Html => "HTML report (*.html)|*.html",
            ExportFormat.Csv => "CSV file (*.csv)|*.csv",
            ExportFormat.Json => "JSON file (*.json)|*.json",
            ExportFormat.Xml => "XML file (*.xml)|*.xml",
            _ => "All files (*.*)|*.*",
        };
    }
}
