using CamoSwitcher.Core;

namespace CamoSwitcher.App;

public sealed partial class MainForm
{
    private readonly GlassButton _aimSettingsButton = MakeButton("Silent aim settings");
    private SilentAimSettings _silentAim = SilentAimSettings.Default;
    private static SilentAimSettings SmokeAimSettings => new(24, 75, 150, AimTargetBone.UpperChest, AimActivation.AimButton, AimGameMode.Both);

    private void ShowSilentAimSettings()
    {
        using var dialog = new SilentAimDialog(_silentAim);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var settings = dialog.Settings;
            SilentAimSettings.Validate(settings);
            var next = _userData with { Preferences = CapturePreferences() with { SilentAim = settings } };
            _configStore.Save(next);
            _userData = next;
            _silentAim = settings;
            _preferencesDirty = false;
            _saveTimer.Stop();
            Log("Silent aim preferences saved locally. In-game silent aim is unavailable and remains inactive.");
        }
        catch (Exception ex) { ReportError(ex); }
    }

    public void VerifySilentAimUi(string previewPath)
    {
        using var dialog = new SilentAimDialog(_silentAim);
        dialog.Show(this);
        Application.DoEvents();
        dialog.VerifyEditing(_silentAim);
        using var bitmap = new Bitmap(dialog.Width, dialog.Height);
        dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
        bitmap.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
        dialog.Close();
    }

    private sealed class SilentAimDialog : Form
    {
        private readonly NumericUpDown _fov = Number(1, 180);
        private readonly NumericUpDown _chance = Number(0, 100);
        private readonly NumericUpDown _distance = Number(1, 1000);
        private readonly ComboBox _bone = Choice("Head", "Upper chest", "Center mass");
        private readonly ComboBox _activation = Choice("Hold aim button", "Hold fire button");
        private readonly ComboBox _mode = Choice("Multiplayer + Zombies", "Multiplayer", "Zombies");
        private readonly CheckBox _visibleOnly = new() { Text = "Visible targets only", Dock = DockStyle.Fill, BackColor = Color.Transparent };
        private readonly CheckBox _enable = new()
        {
            Text = "Enable silent aim", Enabled = false, Checked = false,
            Dock = DockStyle.Fill, BackColor = Color.Transparent, AccessibleName = "Silent aim activation unavailable"
        };
        private readonly GlassButton _reset = MakeButton("Reset defaults");

        public SilentAimSettings Settings => new(
            (int)_fov.Value, (int)_chance.Value, (int)_distance.Value,
            (AimTargetBone)_bone.SelectedIndex, (AimActivation)_activation.SelectedIndex,
            (AimGameMode)_mode.SelectedIndex, _visibleOnly.Checked);

        public SilentAimDialog(SilentAimSettings settings)
        {
            Text = "Silent aim settings";
            Font = new Font("Segoe UI", 10);
            BackColor = Background;
            ForeColor = TextColor;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(620, 580);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var root = Table(1, 4);
            root.Padding = new Padding(20);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            Controls.Add(root);

            var header = Table(1, 3);
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.Controls.Add(MakeLabel("Silent aim settings", 20, TextColor, FontStyle.Bold), 0, 0);
            header.Controls.Add(MakeLabel("Unavailable in-game · settings only", 10, Color.FromArgb(158, 93, 20)), 0, 1);
            header.Controls.Add(_enable, 0, 2);
            root.Controls.Add(header, 0, 0);

            var panel = new GlassPanel { Padding = new Padding(16, 10, 16, 10) };
            var fields = Table(2, 7);
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
            for (var row = 0; row < 7; row++) fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 7));
            AddField(fields, 0, "Game mode", _mode);
            AddField(fields, 1, "Field of view (degrees)", _fov);
            AddField(fields, 2, "Hit chance (%)", _chance);
            AddField(fields, 3, "Maximum distance (m)", _distance);
            AddField(fields, 4, "Target bone", _bone);
            AddField(fields, 5, "Activation preference", _activation);
            fields.Controls.Add(_visibleOnly, 0, 6);
            fields.SetColumnSpan(_visibleOnly, 2);
            panel.Controls.Add(fields);
            root.Controls.Add(panel, 0, 1);

            root.Controls.Add(MakeLabel("These options save locally and with named configs.\nSilent aim stays off even when the game is connected.", 9, Muted), 0, 2);
            var actions = Table(4, 1);
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            var cancel = MakeButton("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            var save = MakeButton("Save settings", true);
            save.Click += (_, _) =>
            {
                SilentAimSettings.Validate(Settings);
                DialogResult = DialogResult.OK;
            };
            _reset.Click += (_, _) => SetSettings(SilentAimSettings.Default);
            actions.Controls.Add(_reset, 0, 0);
            actions.Controls.Add(cancel, 2, 0);
            actions.Controls.Add(save, 3, 0);
            AcceptButton = save;
            CancelButton = cancel;
            root.Controls.Add(actions, 0, 3);
            SetSettings(settings);
        }

        private void SetSettings(SilentAimSettings settings)
        {
            SilentAimSettings.Validate(settings);
            _fov.Value = settings.FieldOfView;
            _chance.Value = settings.HitChance;
            _distance.Value = settings.MaxDistance;
            _bone.SelectedIndex = (int)settings.TargetBone;
            _activation.SelectedIndex = (int)settings.Activation;
            _mode.SelectedIndex = (int)settings.GameMode;
            _visibleOnly.Checked = settings.VisibleOnly;
        }

        public void VerifyEditing(SilentAimSettings expected)
        {
            if (_enable.Enabled || _enable.Checked || Settings != expected)
                throw new InvalidOperationException("The aim dialog must restore preferences and keep unavailable activation disabled.");
            _fov.Value = 42;
            if (Settings.FieldOfView != 42) throw new InvalidOperationException("Editing an aim preference failed.");
            _reset.PerformClick();
            if (Settings != SilentAimSettings.Default) throw new InvalidOperationException("Resetting aim preferences failed.");
            SetSettings(expected);
            foreach (var input in new Control[] { _fov, _chance, _distance, _bone, _activation, _mode, _visibleOnly })
            {
                var parent = input.Parent!;
                if (input.Height < 20 || input.Width < 80 || input.Bottom > parent.ClientSize.Height || input.Right > parent.ClientSize.Width)
                    throw new InvalidOperationException("An aim setting control is clipped.");
            }
        }

        private static void AddField(TableLayoutPanel fields, int row, string label, Control input)
        {
            input.AccessibleName = label;
            fields.Controls.Add(MakeLabel(label, 10, TextColor), 0, row);
            var shell = InputShell(input);
            // InputShell supplies a generic accessible name; retain the field's name.
            input.AccessibleName = label;
            fields.Controls.Add(shell, 1, row);
        }

        private static NumericUpDown Number(int minimum, int maximum) => new()
        {
            Minimum = minimum, Maximum = maximum, DecimalPlaces = 0, TextAlign = HorizontalAlignment.Right,
            BorderStyle = BorderStyle.None, Dock = DockStyle.Fill
        };

        private static ComboBox Choice(params string[] values)
        {
            var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            choice.Items.AddRange(values);
            return choice;
        }
    }
}
