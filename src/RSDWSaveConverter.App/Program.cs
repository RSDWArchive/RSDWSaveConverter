namespace RSDWSaveConverter.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args is ["--render", var outputPath])
        {
            using var form = new MainForm();
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-10_000, -10_000);
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bitmap, form.ClientRectangle);
            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            form.Hide();
            return;
        }

        Application.Run(new MainForm());
    }
}
