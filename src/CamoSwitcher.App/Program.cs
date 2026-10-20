namespace CamoSwitcher.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var form = new MainForm();
        if (args.Length == 2 && args[0] == "--smoke-test")
        {
            form.Show();
            Application.DoEvents();
            form.VerifyUi();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            return;
        }
        Application.Run(form);
    }
}
