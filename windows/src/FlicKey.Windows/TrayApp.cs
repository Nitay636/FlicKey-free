using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace FlicKey.Windows;

internal sealed class TrayApp : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "FlicKey";

    private readonly NotifyIcon _icon;
    private readonly KeyboardMonitor _monitor;
    private readonly ConversionService _service;
    private readonly SynchronizationContext _ui;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _startupItem;

    public TrayApp()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _monitor = new KeyboardMonitor();
        _service = new ConversionService(_monitor);
        _service.Notice += text => Balloon(text);
        _monitor.DoubleShift += () => _ui.Post(async _ =>
        {
            try { await _service.RunAsync(); }
            catch (Exception ex) { Balloon("FlicKey hit an error: " + ex.Message); }
        }, null);

        _enabledItem = new ToolStripMenuItem("Enabled") { Checked = true, CheckOnClick = true };
        _enabledItem.CheckedChanged += (_, _) => _monitor.Enabled = _enabledItem.Checked;

        _startupItem = new ToolStripMenuItem("Start with Windows") { Checked = IsStartupEnabled(), CheckOnClick = true };
        _startupItem.CheckedChanged += (_, _) => SetStartup(_startupItem.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("FlicKey " + Version) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripMenuItem("How to use", null, (_, _) => Balloon(HowTo)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));

        _icon = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "FlicKey - double-tap Shift to fix wrong-layout text",
            ContextMenuStrip = menu,
            Visible = true,
        };
        Balloon(HowTo);
    }

    private const string HowTo = "Typed in the wrong keyboard language? Double-tap Shift to convert it. " +
                                 "With text selected, double-tap Shift converts the selection.";

    private static string Version =>
        typeof(TrayApp).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";

    private void Balloon(string text)
    {
        _icon.BalloonTipTitle = "FlicKey";
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(4000);
    }

    protected override void ExitThreadCore()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _monitor.Dispose();
        base.ExitThreadCore();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunName) is string;
    }

    private static void SetStartup(bool on)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;
        if (on && Environment.ProcessPath is { } path) key.SetValue(RunName, "\"" + path + "\"");
        else key.DeleteValue(RunName, throwOnMissingValue: false);
    }

    private static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var bg = new SolidBrush(Color.FromArgb(0x2B, 0x6C, 0xF0));
            using var path = RoundedRect(new Rectangle(1, 1, 30, 30), 7);
            g.FillPath(bg, path);
            using var font = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
            var size = g.MeasureString("FK", font);
            g.DrawString("FK", font, Brushes.White, (32 - size.Width) / 2, (32 - size.Height) / 2);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
