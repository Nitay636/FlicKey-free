namespace FlicKey.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\FlicKey.SingleInstance", out bool first);
        if (!first) return;   // already running in the tray

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            Application.Run(new TrayApp());
        }
        catch (Exception ex)
        {
            MessageBox.Show("FlicKey could not start:\n\n" + ex.Message, "FlicKey",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
