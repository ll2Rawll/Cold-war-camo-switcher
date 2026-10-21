using System.Diagnostics;
using System.Text.Json;
using CamoSwitcher.Core;

namespace CamoSwitcher.App;

public sealed partial class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(237, 243, 253);
    private static readonly Color Surface = Color.White;
    private static readonly Color TextColor = Color.FromArgb(29, 42, 65);
    private static readonly Color Muted = Color.FromArgb(98, 115, 142);
    private static readonly Color Accent = Color.FromArgb(0, 113, 245);
    private readonly ComboBox _processes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { PlaceholderText = "Search camos…", MaxLength = 100, Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new SmoothGrid();
    private readonly Label _count = MakeLabel("", 9, Muted);
    private readonly Label _name = MakeLabel("Shards", 20, TextColor, FontStyle.Bold);
    private readonly Label _group = MakeLabel("Multiplayer · Spray  /  ID 1", 10, Accent);
    private readonly Label _status = MakeLabel("Start Cold War and open its camo menu.", 10, Muted);
    private readonly GlassButton _refresh = MakeButton("Refresh");
    private readonly GlassButton _scan = MakeButton("Connect", true);
    private readonly GlassButton _cancel = MakeButton("Cancel");
    private readonly GlassButton _disconnect = MakeButton("Disconnect");
    private readonly GlassButton _apply = MakeButton("Apply selected camo", true);
    private readonly GlassButton _restore = MakeButton("Restore original Shards");
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

    public MainForm(string? configDirectory = null)
    {
        Text = "Cold War Studio · Camo Switcher";
        BackColor = Background;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1160, 850);
        MinimumSize = new Size(1020, 780);
        StartPosition = FormStartPosition.CenterScreen;
        _configStore = new ConfigStore(configDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColdWarCamoSwitcher"));
        BuildLayout();
        WireConfigControls();
        _refresh.Click += (_, _) => RefreshProcesses();
        _scan.Click += async (_, _) => await ScanAsync();
        _cancel.Click += (_, _) => { _cancellation?.Cancel(); _status.Text = "Cancelling scan…"; };
        _disconnect.Click += (_, _) => Disconnect();
        _apply.Click += (_, _) => ApplySelected();
        _restore.Click += (_, _) => Restore();
        _category.SelectedIndexChanged += (_, _) => FilterCamos();
        _search.TextChanged += (_, _) => { _filterTimer.Stop(); _filterTimer.Start(); };
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
        _category.Items.AddRange(ConfigStore.Categories);
        _category.SelectedIndex = 0;
        RefreshProcesses();
        InitializeConfigs();
        Log("Ready. Game Pass compatibility is experimental. Scanning does not change game data.");
        _timer.Start();
        UpdateButtons();
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
            _status.Text = "Start Cold War, equip Shards, then refresh and connect. Choose its game process from the list if needed.";
        UpdateButtons();
    }

    private void FilterCamos()
    {
        var category = _category.SelectedItem as string ?? "All camos";
        var query = _search.Text.Trim();
        var selectedId = _preferredCamoId ?? _selected?.Id;
        _preferredCamoId = null;
        var filtered = CamoCatalog.All.Where(c =>
            (!_onlyFavorites || _favorites.Contains(c.Id)) &&
            (category == "All camos" || c.Category.StartsWith(category, StringComparison.Ordinal)) &&
            (c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             c.Category.Contains(query, StringComparison.OrdinalIgnoreCase) || c.Id.ToString().Contains(query, StringComparison.Ordinal))).ToArray();
        _grid.DataSource = filtered;
        _count.Text = $"{filtered.Length} of {CamoCatalog.All.Count} camos";
        if (selectedId is byte id)
        {
            var index = Array.FindIndex(filtered, c => c.Id == id);
            if (index >= 0) _grid.CurrentCell = _grid.Rows[index].Cells[0];
        }
        UpdateSelection();
        QueuePreferencesSave();
    }

    private void UpdateSelection()
    {
        _selected = _grid.CurrentRow?.DataBoundItem as Camo;
        _name.Text = _selected?.Name ?? "No camo selected";
        _group.Text = _selected is null ? "Try a different search" : $"{_selected.Category}  /  ID {_selected.Id}";
        _favorite.Text = _selected is not null && _favorites.Contains(_selected.Id) ? "★" : "☆";
        _favorite.SetActive(_selected is not null && _favorites.Contains(_selected.Id));
        _favorite.AccessibleName = _selected is not null && _favorites.Contains(_selected.Id) ? "Remove from favorites" : "Add to favorites";
        UpdateButtons();
        QueuePreferencesSave();
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
            _status.Text = "Connected. Choose a camo, apply it, then reselect Shards in-game. Game Pass behavior still needs verification.";
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
        _connectionBadge.Text = _busy ? "Scanning…" : usable ? "●  Connected" : "○  Not connected";
        _connectionBadge.ForeColor = usable ? Color.FromArgb(20, 131, 99) : Muted;
        _favorite.Enabled = _selected is not null;
        _saveConfig.Enabled = _selected is not null && !_configSavingBlocked && !string.IsNullOrWhiteSpace(_configName.Text);
        _loadConfig.Enabled = _configs.SelectedItem is SavedConfig;
        _deleteConfig.Enabled = _configs.SelectedItem is SavedConfig;
        _exportConfigs.Enabled = _userData.Configs.Length > 0;
    }

    private void ExportDiagnostics(IWin32Window? owner = null)
    {
        using var picker = new SaveFileDialog { Filter = "JSON report (*.json)|*.json", FileName = $"diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json" };
        if (picker.ShowDialog(owner ?? this) != DialogResult.OK) return;
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
        _activitySummary.Text = message;
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
        _saveTimer.Stop();
        PersistPreferences();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _filterTimer.Dispose();
            _saveTimer.Dispose();
            _tips.Dispose();
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
        FlushSearch();
        if (_grid.Rows.Count != 1 || _selected?.Id != 66) throw new InvalidOperationException("Name search failed.");
        _search.Text = "";
        FlushSearch();
        _category.SelectedItem = "Mastery";
        if (_grid.Rows.Count != 6) throw new InvalidOperationException("Mastery filter failed.");
        _category.SelectedItem = "Pack-a-Punch";
        if (_grid.Rows.Count != 9) throw new InvalidOperationException("Pack-a-Punch filter failed.");
        _category.SelectedItem = "All camos";
        _search.Text = "no-such-camo";
        FlushSearch();
        if (_grid.Rows.Count != 0 || _selected is not null) throw new InvalidOperationException("Empty search failed.");
        _search.Text = "";
        FlushSearch();
        if (_grid.Rows.Count != 121) throw new InvalidOperationException("Catalog reset failed.");
        VerifyConfigUi();
        if (_grid.Width < 380 || _name.Height < 28 || _status.Height < 28 || _configs.Parent!.Height < 27)
            throw new InvalidOperationException("The UI layout must keep the catalog, selected camo and connection status visible.");
        foreach (var control in new Control[] { _scan, _refresh, _cancel, _disconnect, _loadConfig, _saveConfig, _configs, _configName, _category, _search })
        {
            var parent = control.Parent!;
            if (control.Bottom > parent.ClientSize.Height || control.Right > parent.ClientSize.Width || control.Top < 0 || control.Left < 0)
                throw new InvalidOperationException($"A control is clipped in its container: {control.AccessibleName ?? control.Text}.");
        }
    }

    private sealed record ProcessChoice(int Id, string Name)
    {
        public bool IsLikelyGame => Name.StartsWith("BlackOpsColdWar", StringComparison.OrdinalIgnoreCase);
        public override string ToString() => $"{Name}   [PID {Id}]{(IsLikelyGame ? "  · Cold War" : "")}";
    }
}
