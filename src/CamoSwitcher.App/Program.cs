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
                var libraryPreview = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,
                    Path.GetFileNameWithoutExtension(args[1]) + "-library.png");
                CapturePreview(form, libraryPreview);
                form.VerifyUi();
                CapturePreview(form, args[1]);
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

    private static void CapturePreview(Form form, string path)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
