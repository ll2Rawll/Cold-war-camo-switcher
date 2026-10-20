namespace CamoSwitcher.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--smoke-test")
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "cw-ui-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var form = new MainForm(tempDirectory);
                form.Show();
                Application.DoEvents();
                form.VerifyUi();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                form.Hide();
                using var reopened = new MainForm(tempDirectory);
                reopened.Show();
                Application.DoEvents();
                reopened.VerifyRestoredConfigUi();
            }
            finally { if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true); }
            return;
        }
        using var mainForm = new MainForm();
        Application.Run(mainForm);
    }
}
