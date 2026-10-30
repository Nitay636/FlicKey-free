using System.Text;
using FlicKey.Core;

namespace FlicKey.Windows;

// A Windows keyboard layout (HKL) seen through physical scan codes.
internal sealed class WinKeyboardLayout : IKeyboardLayout
{
    // Main typing block: number row, three letter rows, space, and the ISO key.
    private static readonly int[] ScanCodes = BuildScanCodes();

    private static int[] BuildScanCodes()
    {
        var codes = new List<int>();
        codes.AddRange(Enumerable.Range(0x02, 0x0D - 0x02 + 1));
        codes.AddRange(Enumerable.Range(0x10, 0x1B - 0x10 + 1));
        codes.AddRange(Enumerable.Range(0x1E, 0x29 - 0x1E + 1));
        codes.AddRange(Enumerable.Range(0x2B, 0x35 - 0x2B + 1));
        codes.Add(0x39);
        codes.Add(0x56);
        return codes.ToArray();
    }

    public static bool IsPrintableScanCode(int scanCode) => Array.IndexOf(ScanCodes, scanCode) >= 0;

    public IntPtr Hkl { get; }
    public string Id { get; }

    public WinKeyboardLayout(IntPtr hkl)
    {
        Hkl = hkl;
        Id = IdOf(hkl);
    }

    public static string IdOf(IntPtr hkl) => ((long)hkl & 0xFFFFFFFF).ToString("X8");

    public string? Translate(Stroke s)
    {
        uint vk = Native.MapVirtualKeyEx((uint)s.ScanCode, Native.MAPVK_VSC_TO_VK, Hkl);
        if (vk == 0) return null;

        var state = new byte[256];
        if (s.Shift) state[0x10] = 0x80;                       // VK_SHIFT
        if (s.AltGr) { state[0x11] = 0x80; state[0x12] = 0x80; } // VK_CONTROL + VK_MENU
        if (s.CapsLock) state[0x14] = 0x01;                    // VK_CAPITAL toggled

        var buffer = new StringBuilder(8);
        // Flag 0x4: do not change the keyboard state (Windows 10 1607+), so a dead
        // key probed here cannot leak into the user's next keystroke.
        int n = Native.ToUnicodeEx(vk, (uint)s.ScanCode, state, buffer, buffer.Capacity, 0x4, Hkl);
        if (n <= 0) return null;                                // no text, or a dead key
        var text = buffer.ToString(0, n);
        return text.Any(char.IsControl) ? null : text;
    }

    public IEnumerable<Stroke> AllStrokes
    {
        get
        {
            foreach (var (shift, altGr) in new[] { (false, false), (true, false), (false, true), (true, true) })
                foreach (var sc in ScanCodes)
                    yield return new Stroke(sc, shift, altGr, false);
        }
    }
}

internal static class Layouts
{
    // Installed layouts in the order Windows cycles them. IME-style layouts
    // (high word 0xE0xx) are skipped: they have no direct
    // key-to-text mapping.
    public static List<WinKeyboardLayout> Installed()
    {
        int count = Native.GetKeyboardLayoutList(0, null);
        var handles = new IntPtr[count];
        Native.GetKeyboardLayoutList(count, handles);
        var result = new List<WinKeyboardLayout>();
        foreach (var h in handles)
        {
            if (result.Any(l => l.Hkl == h)) continue;
            uint high = (uint)(((long)h >> 16) & 0xFFFF);
            if ((high & 0xF000) == 0xE000) continue;
            result.Add(new WinKeyboardLayout(h));
        }
        return result;
    }

    public static IntPtr ForegroundWindow => Native.GetForegroundWindow();

    public static string? ForegroundLayoutId()
    {
        var hwnd = Native.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        uint thread = Native.GetWindowThreadProcessId(hwnd, out _);
        var hkl = Native.GetKeyboardLayout(thread);
        return hkl == IntPtr.Zero ? null : WinKeyboardLayout.IdOf(hkl);
    }

    public static void Switch(IReadOnlyList<WinKeyboardLayout> all, string id)
    {
        var target = all.FirstOrDefault(l => l.Id == id);
        var hwnd = Native.GetForegroundWindow();
        if (target is null || hwnd == IntPtr.Zero) return;
        Native.PostMessage(hwnd, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, target.Hkl);
    }
}
