using System.Diagnostics;
using System.Text.Json;
using CamoSwitcher.Core;

namespace CamoSwitcher.App;

public sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(18, 23, 29);
    private static readonly Color Surface = Color.FromArgb(27, 34, 42);
    private static readonly Color TextColor = Color.FromArgb(233, 239, 240);
    private static readonly Color Muted = Color.FromArgb(164, 179, 185);
    private static readonly Color Accent = Color.FromArgb(94, 215, 180);
    private readonly ComboBox _processes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { PlaceholderText = "Search by camo name or ID", Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new();
    private readonly Label _count = MakeLabel("", 9, Muted);
    private readonly Label _name = MakeLabel("Shards", 25, TextColor, FontStyle.Bold);
    private readonly Label _group = MakeLabel("Multiplayer · Spray  /  ID 1", 10, Accent);
    private readonly Label _status = MakeLabel("Start Cold War and open its camo menu.", 10, Muted);
    private readonly TextBox _log = new() { ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Button _refresh = MakeButton("Refresh");
    private readonly Button _scan = MakeButton("Scan game", true);
    private readonly Button _cancel = MakeButton("Cancel");
    private readonly Button _disconnect = MakeButton("Disconnect");
    private readonly Button _apply = MakeButton("Use selected camo", true);
    private readonly Button _restore = MakeButton("Restore Shards");
    private readonly Button _export = MakeButton("Save diagnostics");
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };
    private readonly List<string> _messages = [];
    private CamoSession? _session;
    private Camo? _selected;
    private CancellationTokenSource? _cancellation;
    private bool _busy;
    private bool _closeAfterOperation;
    private string? _lastError;
    private string? _gameVersion;
    private string? _gameProcessName;
    private ulong? _moduleSize;

    public MainForm()
    {
        Text = "Cold War · Camo Switcher";
        BackColor = Background;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1160, 850);
        MinimumSize = new Size(1060, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        _refresh.Click += (_, _) => RefreshProcesses();
        _scan.Click += async (_, _) => await ScanAsync();
        _cancel.Click += (_, _) => { _cancellation?.Cancel(); _status.Text = "Cancelling scan…"; };
        _disconnect.Click += (_, _) => Disconnect();
        _apply.Click += (_, _) => ApplySelected();
        _restore.Click += (_, _) => Restore();
        _export.Click += (_, _) => ExportDiagnostics();
        _category.SelectedIndexChanged += (_, _) => FilterCamos();
        _search.TextChanged += (_, _) => FilterCamos();
        _grid.SelectionChanged += (_, _) => UpdateSelection();
        _processes.SelectedIndexChanged += (_, _) => UpdateButtons();
        _timer.Tick += (_, _) =>
        {
            if (!_busy && _session is not null && !_session.IsAlive)
            {
                Log("The game process closed. Select the new process and scan again.");
                _session.Dispose();
                _session = null;
                _status.Text = "Game closed. Refresh the process list to reconnect.";
            }
            UpdateButtons();
        };
        _category.Items.AddRange(["All camos", "Mastery", "Multiplayer", "Zombies", "Pack-a-Punch", "CDL variants"]);
        _category.SelectedIndex = 0;
        RefreshProcesses();
        Log("Ready. Game Pass compatibility is experimental. Scanning does not change game data.");
        _timer.Start();
        UpdateButtons();
    }

    private void BuildLayout()
    {
        var root = Table(1, 5);
        root.Padding = new Padding(24);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        var heading = Table(1, 2);
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        heading.Controls.Add(MakeLabel("COLD WAR  /  CAMO SWITCHER", 23, TextColor, FontStyle.Bold), 0, 0);
        heading.Controls.Add(MakeLabel("121 camo IDs · Mastery, Multiplayer, Zombies, Pack-a-Punch & CDL variants", 10, Muted), 0, 1);
        root.Controls.Add(heading, 0, 0);

        var processBar = Table(5, 2);
        processBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var width in new[] { 90, 122, 82, 112 }) processBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
        processBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        processBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var processLabel = MakeLabel("Select the running Cold War game process", 9, Muted);
        processBar.Controls.Add(processLabel, 0, 0);
        processBar.SetColumnSpan(processLabel, 5);
        processBar.Controls.Add(_processes, 0, 1);
        processBar.Controls.Add(_refresh, 1, 1);
        processBar.Controls.Add(_scan, 2, 1);
        processBar.Controls.Add(_cancel, 3, 1);
        processBar.Controls.Add(_disconnect, 4, 1);
        root.Controls.Add(processBar, 0, 1);

        var body = Table(2, 1);
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        root.Controls.Add(body, 0, 2);
        var catalog = Table(1, 3);
        catalog.Margin = new Padding(0, 0, 18, 0);
        catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        catalog.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var filters = Table(2, 1);
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        filters.Controls.Add(_search, 0, 0);
        filters.Controls.Add(_category, 1, 0);
        catalog.Controls.Add(filters, 0, 0);
        catalog.Controls.Add(_count, 0, 1);
        ConfigureGrid();
        catalog.Controls.Add(_grid, 0, 2);
        body.Controls.Add(catalog, 0, 0);

        var details = Table(1, 10);
        details.BackColor = Surface;
        details.Padding = new Padding(20);
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        details.Controls.Add(MakeLabel("SELECTED CAMO", 9, Muted, FontStyle.Bold), 0, 0);
        details.Controls.Add(_name, 0, 1);
        details.Controls.Add(_group, 0, 2);
        details.Controls.Add(MakeLabel("Equip Shards on your weapon, then scan. Apply a camo here and reselect the Shards option in the game's camo menu to refresh it.", 10, Muted), 0, 3);
        details.Controls.Add(_apply, 0, 4);
        details.Controls.Add(_restore, 0, 5);
        details.Controls.Add(_status, 0, 7);
        details.Controls.Add(_export, 0, 8);
        body.Controls.Add(details, 1, 0);

        var logArea = Table(1, 2);
        logArea.Margin = new Padding(0, 14, 0, 0);
        logArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        logArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        logArea.Controls.Add(MakeLabel("ACTIVITY", 9, Muted, FontStyle.Bold), 0, 0);
        _log.BackColor = Surface;
        _log.ForeColor = Muted;
        _log.BorderStyle = BorderStyle.None;
        _log.Font = new Font("Consolas", 9);
        logArea.Controls.Add(_log, 0, 1);
        root.Controls.Add(logArea, 0, 3);
        root.Controls.Add(MakeLabel("Experimental Game Pass compatibility. Permanent unlocks, online force-save and offline save editing are unverified.", 9, Muted), 0, 4);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = Surface;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Background;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Background, ForeColor = Muted, Font = new Font("Segoe UI", 9) };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface, ForeColor = TextColor,
            SelectionBackColor = Color.FromArgb(43, 75, 71), SelectionForeColor = TextColor,
            Padding = new Padding(7, 0, 0, 0)
        };
        _grid.RowTemplate.Height = 36;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Camo", DataPropertyName = "Name", HeaderText = "CAMO", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", DataPropertyName = "Category", HeaderText = "COLLECTION", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", HeaderText = "ID", Width = 48 });
    }

    private void RefreshProcesses()
    {
        var selectedId = (_processes.SelectedItem as ProcessChoice)?.Id;
        var choices = new List<ProcessChoice>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != Environment.ProcessId && process.Id > 4)
                        choices.Add(new ProcessChoice(process.Id, process.ProcessName));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
        }
        var ordered = choices.OrderByDescending(p => p.IsLikelyGame).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Id).ToArray();
        _processes.DataSource = ordered;
        _processes.SelectedIndex = Array.FindIndex(ordered, p => p.Id == selectedId);
        if (_processes.SelectedIndex < 0) _processes.SelectedIndex = Array.FindIndex(ordered, p => p.IsLikelyGame);
        if (_processes.SelectedIndex < 0 && _session is null)
            _status.Text = "No Cold War process detected. Start the game and refresh, or select its executable from the list.";
        UpdateButtons();
    }

    private void FilterCamos()
    {
        var category = _category.SelectedItem as string ?? "All camos";
        var query = _search.Text.Trim();
        var selectedId = _selected?.Id;
        var filtered = CamoCatalog.All.Where(c =>
            (category == "All camos" || c.Category.StartsWith(category, StringComparison.Ordinal)) &&
            (c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             c.Category.Contains(query, StringComparison.OrdinalIgnoreCase) || c.Id.ToString().Contains(query, StringComparison.Ordinal))).ToArray();
        _grid.DataSource = filtered;
        _count.Text = $"{filtered.Length} of {CamoCatalog.All.Count} camos · Availability depends on the game build";
        if (selectedId is byte id)
        {
            var index = Array.FindIndex(filtered, c => c.Id == id);
            if (index >= 0) _grid.CurrentCell = _grid.Rows[index].Cells[0];
        }
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        _selected = _grid.CurrentRow?.DataBoundItem as Camo;
        _name.Text = _selected?.Name ?? "No camo selected";
        _group.Text = _selected is null ? "Try a different search" : $"{_selected.Category}  /  ID {_selected.Id}";
        UpdateButtons();
    }

    private async Task ScanAsync()
    {
        if (_busy || _session is not null || _processes.SelectedItem is not ProcessChoice choice) return;
        _busy = true;
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        _lastError = null;
        _gameProcessName = choice.Name;
        _gameVersion = null;
        _moduleSize = null;
        _status.Text = "Scanning loaded game data…";
        UpdateButtons();
        Log($"Scanning {choice.Name}. No game data is changed by a scan.");
        var progress = new Progress<ScanProgress>(p =>
        {
            if (_busy) _status.Text = $"Scanning… {p.BytesRead / (1024.0 * 1024):N1} MB read. Cancel is available.";
        });
        try
        {
            var result = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var memory = Win32ProcessMemory.Open(choice.Id);
                try
                {
                    var session = CamoSession.Connect(memory, token, progress);
                    return (Session: session, memory.FileVersion, memory.ModuleSize);
                }
                catch { memory.Dispose(); throw; }
            }, token);
            _session = result.Session;
            _gameVersion = result.FileVersion;
            _moduleSize = result.ModuleSize;
            _status.Text = "One Shards signature found. Select a camo and apply it. Game Pass behavior still needs in-game verification.";
            Log("Connected: one signature match. Original option saved for restoration.");
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Scan cancelled. No game data was changed.";
            Log(_status.Text);
        }
        catch (Exception ex) { ReportError(ex); }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            _busy = false;
            UpdateButtons();
            if (_closeAfterOperation) BeginInvoke(Close);
        }
    }

    private void ApplySelected()
    {
        if (_session is null || _selected is null) return;
        try
        {
            _session.Apply(_selected.Id);
            _lastError = null;
            _status.Text = $"Option set to {_selected.Name}. Reselect Shards in the game to refresh. Saving to your profile has not been verified.";
            Log($"Camo option set to {_selected.Name} (ID {_selected.Id}); memory read-back verified.");
        }
        catch (Exception ex) { ReportError(ex); }
        UpdateButtons();
    }

    private bool Restore()
    {
        if (_session is null || !_session.IsModified) return true;
        try
        {
            _session.Restore();
            _status.Text = "Shards option restored. Refresh the camo menu in the game.";
            Log("Original Shards option restored; memory read-back verified.");
            return true;
        }
        catch (Exception ex) { ReportError(ex); return false; }
        finally { UpdateButtons(); }
    }

    private void Disconnect()
    {
        if (_busy || _session is null) return;
        var restored = !_session.IsFaulted && _session.IsAlive && Restore();
        _session.Dispose();
        _session = null;
        _status.Text = restored ? "Disconnected. Scan to connect again." : "Disconnected. Restart the game before reconnecting; restoration was not verified.";
        Log(_status.Text);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var connected = _session is not null;
        var usable = connected && _session!.IsAlive && !_session.IsFaulted;
        _scan.Enabled = !_busy && !connected && _processes.SelectedItem is ProcessChoice;
        _refresh.Enabled = !_busy && !connected;
        _processes.Enabled = !_busy && !connected;
        _cancel.Enabled = _busy;
        _disconnect.Enabled = !_busy && connected;
        _apply.Enabled = !_busy && usable && _selected is not null;
        _restore.Enabled = !_busy && usable && _session!.IsModified;
    }

    private void ExportDiagnostics()
    {
        using var picker = new SaveFileDialog { Filter = "JSON report (*.json)|*.json", FileName = $"diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var report = new
            {
                schemaVersion = 1,
                capturedAtUtc = DateTime.UtcNow,
                appVersion = Application.ProductVersion,
                targetEdition = "PC Xbox / Game Pass (user reported)",
                compatibility = "Unverified on the target Game Pass build",
                game = new { processName = _gameProcessName, fileVersion = _gameVersion, moduleSize = _moduleSize },
                connection = new { connected = _session is not null, faulted = _session?.IsFaulted, currentCamoId = _session?.CurrentId },
                lastError = _lastError,
                activity = _messages.ToArray()
            };
            File.WriteAllText(picker.FileName, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Log("Diagnostics saved. Review the JSON before sharing it.");
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void ReportError(Exception ex)
    {
        _lastError = ex.Message;
        _status.Text = ex.Message;
        Log($"Error: {ex.Message}");
    }

    private void Log(string message)
    {
        _messages.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        if (_messages.Count > 250) _messages.RemoveAt(0);
        _log.Text = string.Join(Environment.NewLine, _messages);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            _closeAfterOperation = true;
            _cancellation?.Cancel();
            e.Cancel = true;
            return;
        }
        if (_session is { IsAlive: true, IsFaulted: false, IsModified: true } && !Restore())
            MessageBox.Show(this, "Shards restoration could not be verified. Restart the game to discard this session's memory changes.", "Restoration incomplete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _session?.Dispose();
        _timer.Stop();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _session?.Dispose();
            _cancellation?.Dispose();
        }
        base.Dispose(disposing);
    }

    public void VerifyUi()
    {
        if (_grid.Rows.Count != 121 || _apply.Enabled || _restore.Enabled)
            throw new InvalidOperationException("Initial UI state is incorrect.");
        _search.Text = "Dark Aether";
        if (_grid.Rows.Count != 1 || _selected?.Id != 66) throw new InvalidOperationException("Name search failed.");
        _search.Text = "";
        _category.SelectedItem = "Mastery";
        if (_grid.Rows.Count != 6) throw new InvalidOperationException("Mastery filter failed.");
        _category.SelectedItem = "Pack-a-Punch";
        if (_grid.Rows.Count != 9) throw new InvalidOperationException("Pack-a-Punch filter failed.");
        _category.SelectedItem = "All camos";
        _search.Text = "no-such-camo";
        if (_grid.Rows.Count != 0 || _selected is not null) throw new InvalidOperationException("Empty search failed.");
        _search.Text = "";
        if (_grid.Rows.Count != 121) throw new InvalidOperationException("Catalog reset failed.");
        if (_grid.Width < 400 || _name.Height < 50) throw new InvalidOperationException("The UI layout is too small.");
    }

    private static TableLayoutPanel Table(int columns, int rows) => new()
    {
        ColumnCount = columns, RowCount = rows, Dock = DockStyle.Fill,
        Margin = Padding.Empty, Padding = Padding.Empty
    };

    private static Label MakeLabel(string text, float size, Color color, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, Font = new Font("Segoe UI", size, style), ForeColor = color,
        Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft
    };

    private static Button MakeButton(string text, bool primary = false)
    {
        var button = new Button
        {
            Text = text, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Surface, ForeColor = primary ? Background : TextColor,
            Margin = new Padding(3), Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private sealed record ProcessChoice(int Id, string Name)
    {
        public bool IsLikelyGame => Name.StartsWith("BlackOpsColdWar", StringComparison.OrdinalIgnoreCase);
        public override string ToString() => $"{Name}   [PID {Id}]{(IsLikelyGame ? "  · Cold War" : "")}";
    }
}
