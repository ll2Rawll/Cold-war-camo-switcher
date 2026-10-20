using System.Drawing.Drawing2D;

namespace CamoSwitcher.App;

public sealed partial class MainForm
{
    private void BuildLayout()
    {
        var root = Table(1, 5);
        root.Padding = new Padding(18);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        Controls.Add(root);

        var heading = Table(2, 2);
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 206));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        heading.Controls.Add(MakeLabel("Cold War Studio", 25, TextColor, FontStyle.Bold), 0, 0);
        heading.Controls.Add(MakeLabel("Your camos. Your favorites. Your setup.", 10, Muted), 0, 1);
        heading.Controls.Add(_connectionBadge, 1, 0);
        _connectionBadge.TextAlign = ContentAlignment.MiddleRight;
        root.Controls.Add(heading, 0, 0);

        var connection = new GlassPanel { Padding = new Padding(17, 12, 17, 11), Margin = new Padding(0, 0, 0, 8) };
        var connectionRows = Table(1, 3);
        connectionRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        connectionRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        connectionRows.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        connectionRows.Controls.Add(MakeLabel("01   CONNECT TO COLD WAR", 8.5f, Muted, FontStyle.Bold), 0, 0);
        var processBar = Table(5, 1);
        processBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var width in new[] { 82, 108, 76, 98 }) processBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
        processBar.Controls.Add(InputShell(_processes), 0, 0);
        processBar.Controls.Add(_refresh, 1, 0);
        processBar.Controls.Add(_scan, 2, 0);
        processBar.Controls.Add(_cancel, 3, 0);
        processBar.Controls.Add(_disconnect, 4, 0);
        connectionRows.Controls.Add(processBar, 0, 1);
        connectionRows.Controls.Add(_status, 0, 2);
        connection.Controls.Add(connectionRows);
        root.Controls.Add(connection, 0, 1);

        var body = Table(2, 1);
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        var library = new GlassPanel { Margin = new Padding(0, 0, 10, 0) };
        var catalog = Table(1, 4);
        catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        catalog.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        catalog.Controls.Add(MakeLabel("02   Camo library", 15, TextColor, FontStyle.Bold), 0, 0);
        var filters = Table(2, 1);
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        filters.Controls.Add(InputShell(_search), 0, 0);
        filters.Controls.Add(InputShell(_category), 1, 0);
        catalog.Controls.Add(filters, 0, 1);
        var favorites = Table(2, 1);
        favorites.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        favorites.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        favorites.Controls.Add(_count, 0, 0);
        favorites.Controls.Add(_favoritesButton, 1, 0);
        catalog.Controls.Add(favorites, 0, 2);
        ConfigureGrid();
        catalog.Controls.Add(_grid, 0, 3);
        library.Controls.Add(catalog);
        body.Controls.Add(library, 0, 0);

        var right = Table(1, 2);
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
        var selection = new GlassPanel { Padding = new Padding(17, 12, 17, 12), Margin = new Padding(0, 0, 0, 8) };
        var details = Table(1, 6);
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        var selectedHeading = Table(2, 1);
        selectedHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        selectedHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        selectedHeading.Controls.Add(MakeLabel("03   READY TO APPLY", 8.5f, Muted, FontStyle.Bold), 0, 0);
        selectedHeading.Controls.Add(_favorite, 1, 0);
        details.Controls.Add(selectedHeading, 0, 0);
        details.Controls.Add(_name, 0, 1);
        details.Controls.Add(_group, 0, 2);
        details.Controls.Add(_apply, 0, 3);
        details.Controls.Add(_restore, 0, 4);
        details.Controls.Add(MakeLabel("After applying, reselect Shards in-game.", 8.5f, Muted), 0, 5);
        selection.Controls.Add(details);
        right.Controls.Add(selection, 0, 0);

        var configs = new GlassPanel { Padding = new Padding(17, 12, 17, 12) };
        var configRows = Table(1, 4);
        configRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        configRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        configRows.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        configRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        configRows.Controls.Add(MakeLabel("Saved configs", 13, TextColor, FontStyle.Bold), 0, 0);
        var loadRow = Table(2, 1);
        loadRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        loadRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        loadRow.Controls.Add(InputShell(_configs), 0, 0);
        loadRow.Controls.Add(_loadConfig, 1, 0);
        configRows.Controls.Add(loadRow, 0, 1);
        var saveRow = Table(2, 1);
        saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        saveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        saveRow.Controls.Add(InputShell(_configName), 0, 0);
        saveRow.Controls.Add(_saveConfig, 1, 0);
        configRows.Controls.Add(saveRow, 0, 2);
        var manage = Table(3, 1);
        for (var i = 0; i < 3; i++) manage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        manage.Controls.Add(_importConfigs, 0, 0);
        manage.Controls.Add(_exportConfigs, 1, 0);
        manage.Controls.Add(_deleteConfig, 2, 0);
        configRows.Controls.Add(manage, 0, 3);
        configs.Controls.Add(configRows);
        right.Controls.Add(configs, 0, 1);
        body.Controls.Add(right, 1, 0);
        root.Controls.Add(body, 0, 2);

        var activity = Table(2, 1);
        activity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        activity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        activity.Controls.Add(_activitySummary, 0, 0);
        activity.Controls.Add(_activityButton, 1, 0);
        root.Controls.Add(activity, 0, 3);
        root.Controls.Add(MakeLabel("Game Pass support is experimental. Configs save your choices locally; permanent game unlocks are unverified.", 8.5f, Muted), 0, 4);
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
        _grid.BindingContext = new BindingContext();
        _grid.BackgroundColor = Color.FromArgb(249, 251, 255);
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Color.FromArgb(235, 240, 249);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(245, 248, 254), ForeColor = Muted, Font = new Font("Segoe UI", 8.5f) };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(249, 251, 255), ForeColor = TextColor,
            SelectionBackColor = Color.FromArgb(222, 237, 255), SelectionForeColor = Color.FromArgb(0, 89, 194),
            Padding = new Padding(8, 0, 0, 0)
        };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.White;
        _grid.RowTemplate.Height = 38;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Camo", DataPropertyName = "Name", HeaderText = "CAMO", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 57, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", DataPropertyName = "Category", HeaderText = "COLLECTION", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 43, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", HeaderText = "ID", Width = 42, SortMode = DataGridViewColumnSortMode.NotSortable });
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var background = new LinearGradientBrush(ClientRectangle, Color.FromArgb(235, 244, 255), Color.FromArgb(235, 232, 250), 65);
        e.Graphics.FillRectangle(background, ClientRectangle);
        using var path = new GraphicsPath();
        path.AddEllipse(-180, -190, 780, 590);
        using var glow = new PathGradientBrush(path) { CenterColor = Color.FromArgb(150, 198, 222, 255), SurroundColors = [Color.Transparent] };
        e.Graphics.FillPath(glow, path);
    }

    private static TableLayoutPanel Table(int columns, int rows) => new()
    {
        ColumnCount = columns, RowCount = rows, Dock = DockStyle.Fill,
        BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty
    };

    private static Label MakeLabel(string text, float size, Color color, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text, Font = new Font("Segoe UI", size, style), ForeColor = color,
        BackColor = Color.Transparent, UseMnemonic = false,
        Dock = DockStyle.Fill, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft
    };

    private static GlassButton MakeButton(string text, bool primary = false) => new(primary)
    {
        Text = text, Dock = DockStyle.Fill, Margin = new Padding(2),
        AccessibleName = text, Font = new Font("Segoe UI", 9)
    };

    private static GlassPanel InputShell(Control input)
    {
        var shell = new GlassPanel(17, false) { Padding = new Padding(10, 7, 10, 5), Margin = new Padding(2) };
        input.BackColor = Color.FromArgb(253, 254, 255);
        input.ForeColor = TextColor;
        input.Dock = DockStyle.None;
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.AccessibleName = input is TextBox text ? text.PlaceholderText : "Choose an option";
        if (input is TextBox box) box.BorderStyle = BorderStyle.None;
        if (input is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
        shell.Layout += (_, _) => input.Bounds = new Rectangle(shell.Padding.Left,
            Math.Max(0, (shell.ClientSize.Height - input.PreferredSize.Height) / 2),
            Math.Max(1, shell.ClientSize.Width - shell.Padding.Horizontal), input.PreferredSize.Height);
        shell.Controls.Add(input);
        return shell;
    }
}
