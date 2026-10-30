using FlicKey.Core;

namespace FlicKey.Windows;

// Runs one fix when the user double-taps Shift: replace the text just typed, or,
// when nothing was typed since the caret last moved, convert the selection.
internal sealed class ConversionService
{
    private readonly KeyboardMonitor _monitor;
    private bool _busy;

    public event Action<string>? Notice;

    public ConversionService(KeyboardMonitor monitor) { _monitor = monitor; }

    public async Task RunAsync()
    {
        if (_busy) return;
        _busy = true;
        _monitor.Suspended = true;
        try
        {
            var installed = Layouts.Installed();
            if (installed.Count < 2)
            {
                Notice?.Invoke("Add a second keyboard layout in Windows Settings > Time & language > Language & region.");
                return;
            }
            var layouts = installed.Cast<IKeyboardLayout>().ToList();
            var current = Layouts.ForegroundLayoutId();
            var buffer = _monitor.Buffer;

            ConversionResult? result;
            if (!buffer.IsEmpty)
            {
                result = LayoutConverter.FromTrace(buffer.Entries, layouts)
                      ?? LayoutConverter.FromText(buffer.Text, layouts, current);
                if (result is null) return;
                int keys = buffer.Count;
                Injector.Backspaces(keys);
                Injector.TypeText(result.Text);
                buffer.Adopt(result.Text, result.TargetLayoutId, result.Strokes);
            }
            else
            {
                result = await ConvertSelectionAsync(layouts, current);
                if (result is null) return;
                buffer.Reset();
            }

            Layouts.Switch(installed, result.TargetLayoutId);
            await Task.Delay(60);   // let the target app drain the injected keys
        }
        finally
        {
            _monitor.Suspended = false;
            _busy = false;
        }
    }

    private static async Task<ConversionResult?> ConvertSelectionAsync(IReadOnlyList<IKeyboardLayout> layouts, string? current)
    {
        var saved = TryGetText();
        var sentinel = "flickey-" + Guid.NewGuid();
        if (!TrySetText(sentinel)) return null;
        try
        {
            Injector.CtrlCombo(0x43);   // Ctrl+C
            string? copied = null;
            for (int i = 0; i < 8; i++)
            {
                await Task.Delay(50);
                copied = TryGetText();
                if (!string.IsNullOrEmpty(copied) && copied != sentinel) break;
            }
            if (string.IsNullOrEmpty(copied) || copied == sentinel) return null;   // nothing selected

            var result = LayoutConverter.FromText(copied, layouts, current);
            if (result is null) return null;
            if (!TrySetText(result.Text)) return null;
            Injector.CtrlCombo(0x56);   // Ctrl+V
            await Task.Delay(150);      // the paste must read the clipboard before we restore it
            return result;
        }
        finally
        {
            if (saved is not null) TrySetText(saved);
            else TryClear();
        }
    }

    private static string? TryGetText()
    {
        for (int i = 0; i < 5; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch { Thread.Sleep(20); }
        }
        return null;
    }

    private static bool TrySetText(string text)
    {
        for (int i = 0; i < 5; i++)
        {
            try { Clipboard.SetDataObject(text, true); return true; }
            catch { Thread.Sleep(20); }
        }
        return false;
    }

    private static void TryClear()
    {
        try { Clipboard.Clear(); } catch { }
    }
}
