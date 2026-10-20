using CamoSwitcher.Core;

namespace CamoSwitcher.App;

public sealed partial class MainForm
{
    private readonly ConfigStore _configStore;
    private UserData _userData = UserData.Empty;
    private readonly HashSet<byte> _favorites = [];
    private readonly ComboBox _configs = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox _configName = new() { PlaceholderText = "Name this setup…", MaxLength = 48, Dock = DockStyle.Fill };
    private readonly GlassButton _saveConfig = MakeButton("Save", true);
    private readonly GlassButton _loadConfig = MakeButton("Load");
    private readonly GlassButton _importConfigs = MakeButton("Import");
    private readonly GlassButton _exportConfigs = MakeButton("Export");
    private readonly GlassButton _deleteConfig = MakeButton("Delete");
    private readonly GlassButton _favorite = MakeButton("☆");
    private readonly GlassButton _favoritesButton = MakeButton("Favorites");
    private readonly GlassButton _activityButton = MakeButton("Activity");
    private readonly Label _connectionBadge = MakeLabel("○  Not connected", 10, Muted);
    private readonly Label _activitySummary = MakeLabel("Ready to connect", 9, Muted);
    private readonly System.Windows.Forms.Timer _filterTimer = new() { Interval = 160 };
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 600 };
    private readonly ToolTip _tips = new() { AutoPopDelay = 8000 };
    private bool _ready;
    private bool _restoringPreferences;
    private bool _onlyFavorites;
    private bool _preferencesDirty;
    private bool _configSavingBlocked;
    private byte? _preferredCamoId;

    private void WireConfigControls()
    {
        _tips.SetToolTip(_favorite, "Add or remove this camo from your favorites.");
        _tips.SetToolTip(_saveConfig, "Save the chosen camo with the name you entered. The same name updates an existing config.");
        _tips.SetToolTip(_loadConfig, "Prepare the selected config. Apply it in-game when you are ready.");
        _tips.SetToolTip(_importConfigs, "Merge a JSON config library and its favorites.");
        _tips.SetToolTip(_exportConfigs, "Export all named configs and favorites to a JSON file.");
        _processes.AccessibleName = "Running Cold War game process";
        _configs.AccessibleName = "Saved configs";
        _category.AccessibleName = "Camo collection";
        _saveConfig.Click += (_, _) => SaveNamedConfig();
        _configName.TextChanged += (_, _) => UpdateButtons();
        _loadConfig.Click += (_, _) => LoadNamedConfig();
        _importConfigs.Click += (_, _) => ImportConfigs();
        _exportConfigs.Click += (_, _) => ExportConfigs();
        _deleteConfig.Click += (_, _) => DeleteConfig();
        _favorite.Click += (_, _) =>
        {
            if (_selected is null) return;
            if (!_favorites.Add(_selected.Id)) _favorites.Remove(_selected.Id);
            if (_onlyFavorites) FilterCamos(); else UpdateSelection();
            QueuePreferencesSave();
        };
        _favoritesButton.Click += (_, _) =>
        {
            _onlyFavorites = !_onlyFavorites;
            _favoritesButton.SetActive(_onlyFavorites);
            FilterCamos();
        };
        _configs.SelectedIndexChanged += (_, _) =>
        {
            if (_configs.SelectedItem is SavedConfig selected) _configName.Text = selected.Name;
            UpdateButtons();
            QueuePreferencesSave();
        };
        _filterTimer.Tick += (_, _) => FlushSearch();
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); PersistPreferences(); };
        _activityButton.Click += (_, _) => ShowActivity();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.F) { _search.Focus(); _search.SelectAll(); }
        else if (e.Control && e.KeyCode == Keys.S && _saveConfig.Enabled) _saveConfig.PerformClick();
        else if (e.KeyCode == Keys.F5 && _refresh.Enabled) _refresh.PerformClick();
        else if (e.KeyCode == Keys.Escape && _busy) _cancellation?.Cancel();
        else { base.OnKeyDown(e); return; }
        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    private void InitializeConfigs()
    {
        _restoringPreferences = true;
        try
        {
            var result = _configStore.Load();
            _userData = result.Data;
            _favorites.UnionWith(_userData.Preferences.FavoriteIds);
            _onlyFavorites = _userData.Preferences.FavoritesOnly;
            _favoritesButton.SetActive(_onlyFavorites);
            _search.Text = _userData.Preferences.Search;
            _category.SelectedItem = _userData.Preferences.Category;
            _preferredCamoId = _userData.Preferences.SelectedCamoId;
            FlushSearch();
            RefreshConfigs(_userData.Preferences.LastConfigId);
            if (result.Warning is not null) Log(result.Warning);
        }
        catch (Exception ex)
        {
            _configSavingBlocked = true;
            Log($"Saved configs could not be opened: {ex.Message}. Existing data was kept.");
        }
        finally { _restoringPreferences = false; _ready = true; }
    }

    private void FlushSearch()
    {
        _filterTimer.Stop();
        FilterCamos();
    }

    private UserPreferences CapturePreferences() => new(
        _selected?.Id ?? _userData.Preferences.SelectedCamoId,
        _category.SelectedItem as string ?? "All camos", _search.Text[..Math.Min(_search.Text.Length, 100)],
        _onlyFavorites, _favorites.Order().ToArray(), (_configs.SelectedItem as SavedConfig)?.Id);

    private void QueuePreferencesSave()
    {
        if (!_ready || _restoringPreferences || _configSavingBlocked) return;
        _preferencesDirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void PersistPreferences()
    {
        if (!_preferencesDirty || _configSavingBlocked || !_ready) return;
        try
        {
            var next = _userData with { Preferences = CapturePreferences() };
            _configStore.Save(next);
            _userData = next;
            _preferencesDirty = false;
        }
        catch (Exception ex) { ReportError(new IOException($"Your menu preferences could not be saved: {ex.Message}", ex)); }
    }

    private void SaveNamedConfig()
    {
        if (_selected is null || _configSavingBlocked) return;
        try
        {
            var next = ConfigStore.Upsert(_userData with { Preferences = CapturePreferences() },
                _configName.Text, _selected.Id, _category.SelectedItem as string ?? "All camos", _onlyFavorites);
            var saved = next.Configs.Single(c => c.Name.Equals(_configName.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            next = next with { Preferences = next.Preferences with { LastConfigId = saved.Id } };
            _configStore.Save(next);
            _userData = next;
            RefreshConfigs(saved.Id);
            _preferencesDirty = false;
            Log($"Saved “{saved.Name}”. Load it any time to prepare this camo selection.");
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void LoadNamedConfig()
    {
        if (_configs.SelectedItem is not SavedConfig config) return;
        _restoringPreferences = true;
        try
        {
            _search.Text = "";
            var camo = CamoCatalog.All.Single(c => c.Id == config.CamoId);
            _category.SelectedItem = camo.Category.StartsWith(config.Category, StringComparison.Ordinal) ? config.Category : "All camos";
            _onlyFavorites = config.FavoritesOnly && _favorites.Contains(config.CamoId);
            _favoritesButton.SetActive(_onlyFavorites);
            _preferredCamoId = config.CamoId;
            FlushSearch();
            Log($"Loaded “{config.Name}”. Use Apply selected camo when you are connected and ready.");
        }
        finally { _restoringPreferences = false; QueuePreferencesSave(); }
    }

    private void RefreshConfigs(Guid? selectedId = null)
    {
        _configs.DisplayMember = nameof(SavedConfig.Name);
        _configs.DataSource = _userData.Configs;
        _configs.SelectedIndex = Array.FindIndex(_userData.Configs, c => c.Id == selectedId);
        if (_configs.SelectedIndex < 0) _configName.Text = "";
        UpdateButtons();
    }

    private void ImportConfigs()
    {
        using var picker = new OpenFileDialog { Filter = "Config library (*.json)|*.json", Title = "Import saved configs" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var imported = ConfigStore.Read(picker.FileName);
            var next = ConfigStore.Merge(_userData with { Preferences = CapturePreferences() }, imported);
            _configStore.Save(next);
            _userData = next;
            _favorites.Clear();
            _favorites.UnionWith(next.Preferences.FavoriteIds);
            _configSavingBlocked = false;
            RefreshConfigs();
            UpdateSelection();
            Log($"Imported {imported.Configs.Length} configs. Matching names are updated; game memory is unchanged.");
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void ExportConfigs()
    {
        using var picker = new SaveFileDialog { Filter = "Config library (*.json)|*.json", FileName = "ColdWar-configs.json" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ConfigStore.Write(picker.FileName, _userData with { Preferences = CapturePreferences() });
            Log("Config library exported, including your favorites.");
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void DeleteConfig()
    {
        if (_configs.SelectedItem is not SavedConfig config) return;
        if (MessageBox.Show(this, $"Delete the saved config “{config.Name}”?", "Delete config", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        try
        {
            var next = _userData with
            {
                Configs = _userData.Configs.Where(c => c.Id != config.Id).ToArray(),
                Preferences = CapturePreferences() with { LastConfigId = null }
            };
            _configStore.Save(next);
            _userData = next;
            RefreshConfigs();
            Log($"Deleted “{config.Name}”.");
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void ShowActivity()
    {
        using var dialog = new Form
        {
            Text = "Activity and diagnostics", StartPosition = FormStartPosition.CenterParent,
            Size = new Size(780, 420), BackColor = Background, Font = Font, MinimizeBox = false, MaximizeBox = false
        };
        var layout = Table(1, 2);
        layout.Padding = new Padding(18);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var text = new TextBox { Multiline = true, ReadOnly = true, Text = string.Join(Environment.NewLine, _messages), ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, BackColor = Surface, ForeColor = TextColor, BorderStyle = BorderStyle.None };
        var export = MakeButton("Save diagnostic report");
        export.Click += (_, _) => ExportDiagnostics(dialog);
        layout.Controls.Add(text, 0, 0);
        layout.Controls.Add(export, 0, 1);
        dialog.Controls.Add(layout);
        dialog.ShowDialog(this);
    }

    private void VerifyConfigUi()
    {
        _search.Text = "Dark Aether";
        FlushSearch();
        _favorite.PerformClick();
        if (!_favorites.Contains(66)) throw new InvalidOperationException("Favorite action failed.");
        _configName.Text = "Dark Aether";
        _saveConfig.PerformClick();
        if (_userData.Configs.Length != 1 || _userData.Configs[0].CamoId != 66)
            throw new InvalidOperationException("Saving a named config failed.");
        _search.Text = "Shards";
        FlushSearch();
        _loadConfig.PerformClick();
        if (_selected?.Id != 66 || _apply.Enabled) throw new InvalidOperationException("Loading a config must prepare a camo without requiring or changing a game connection.");
        _favoritesButton.PerformClick();
        if (_grid.Rows.Count != 1) throw new InvalidOperationException("Favorite filter failed.");
        _favoritesButton.PerformClick();
        PersistPreferences();
        var reloaded = new ConfigStore(Path.GetDirectoryName(_configStore.FilePath)!).Load().Data;
        if (reloaded.Configs.Length != 1 || reloaded.Preferences.SelectedCamoId != 66 || !reloaded.Preferences.FavoriteIds.Contains((byte)66))
            throw new InvalidOperationException("Config and preference persistence failed.");
    }

    public void VerifyRestoredConfigUi()
    {
        if (_selected?.Id != 66 || !_favorites.Contains(66) || _configs.SelectedItem is not SavedConfig { CamoId: 66 })
            throw new InvalidOperationException("Saved setup was not restored after restarting the app.");
    }
}
