using System.Runtime.InteropServices;
using FlicKey.Core;

namespace FlicKey.Windows;

// Global low-level keyboard + mouse hooks. Observes only (never swallows a key):
// it keeps the typing buffer current and raises DoubleShift. Must be created on
// a thread with a message loop (the UI thread).
internal sealed class KeyboardMonitor : IDisposable
{
    private const uint VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCTRL = 0xA2, VK_RCTRL = 0xA3,
                       VK_LMENU = 0xA4, VK_RMENU = 0xA5, VK_LWIN = 0x5B, VK_RWIN = 0x5C,
                       VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_CAPITAL = 0x14;
    private const int SCAN_BACKSPACE = 0x0E;

    public TypingBuffer Buffer { get; } = new();
    public bool Enabled { get; set; } = true;
    public bool Suspended { get; set; }
    public event Action? DoubleShift;

    private readonly Native.HookProc _keyProc;
    private readonly Native.HookProc _mouseProc;
    private IntPtr _keyHook, _mouseHook;
    private readonly DoubleTapStateMachine _machine = new();
    private bool _lShift, _rShift, _ctrl, _alt, _win;
    private IntPtr _lastForeground;

    public KeyboardMonitor()
    {
        _keyProc = KeyCallback;
        _mouseProc = MouseCallback;
        var module = Native.GetModuleHandle(null);
        _keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _keyProc, module, 0);
        _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, module, 0);
        if (_keyHook == IntPtr.Zero)
            throw new InvalidOperationException("Could not install the keyboard hook (error " + Marshal.GetLastWin32Error() + ").");
    }

    public void Dispose()
    {
        if (_keyHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_keyHook); _keyHook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is Native.WM_LBUTTONDOWN or Native.WM_RBUTTONDOWN or Native.WM_MBUTTONDOWN)
                Buffer.Reset();   // the caret moved
        }
        return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var k = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                // Our own SendInput carries the injected flag; ignore it entirely.
                if ((k.flags & Native.LLKHF_INJECTED) == 0)
                {
                    int msg = (int)wParam;
                    bool down = msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
                    bool up = msg is Native.WM_KEYUP or Native.WM_SYSKEYUP;
                    if (down || up) Handle(k, down);
                }
            }
            catch
            {
                // A hook must never throw into Windows; drop the event.
            }
        }
        return Native.CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    private void Handle(Native.KBDLLHOOKSTRUCT k, bool down)
    {
        // Modifier state is tracked even while paused, so a key released during a
        // fix can never leave Shift "stuck" down; only the actions are gated.
        bool active = Enabled && !Suspended;
        double time = k.time / 1000.0;
        bool shiftBefore = _lShift || _rShift;

        switch (k.vkCode)
        {
            case VK_LSHIFT: _lShift = down; break;
            case VK_RSHIFT: _rShift = down; break;
            case VK_LCTRL: case VK_RCTRL: _ctrl = down; break;
            case VK_LMENU: case VK_RMENU: _alt = down; break;
            case VK_LWIN: case VK_RWIN: _win = down; break;
            case VK_SHIFT: case VK_CONTROL: case VK_MENU: return;   // generic codes; the L/R ones carry the state
            default:
                if (down && active) OnKeyDown(k);
                return;
        }

        bool shiftNow = _lShift || _rShift;
        bool otherMods = _ctrl || _alt || _win;
        bool isShiftKey = k.vkCode is VK_LSHIFT or VK_RSHIFT;

        if (isShiftKey)
        {
            if (shiftNow && !shiftBefore) _machine.ShiftDown(otherMods, time);
            else if (!shiftNow && shiftBefore && _machine.ShiftUp(otherMods, time) && active) DoubleShift?.Invoke();
        }
        else if (down)
        {
            _machine.OtherModifierPressed();
        }
    }

    private void OnKeyDown(Native.KBDLLHOOKSTRUCT k)
    {
        _machine.KeyPressed();

        var foreground = Native.GetForegroundWindow();
        if (foreground != _lastForeground) { _lastForeground = foreground; Buffer.Reset(); }

        bool altGr = _ctrl && _alt;                    // AltGr arrives as Ctrl+Alt
        if (_win || (_ctrl ^ _alt))                    // a shortcut, not typing
        {
            Buffer.Reset();
            return;
        }

        int scan = (int)k.scanCode;
        bool extended = (k.flags & Native.LLKHF_EXTENDED) != 0;

        if (!extended && scan == SCAN_BACKSPACE) { Buffer.Backspace(); return; }
        if (extended || !WinKeyboardLayout.IsPrintableScanCode(scan)) { Buffer.Reset(); return; }

        var layoutId = Layouts.ForegroundLayoutId();
        var layout = layoutId is null ? null : Layouts.Installed().FirstOrDefault(l => l.Id == layoutId);
        if (layout is null) { Buffer.Reset(); return; }

        var stroke = new Stroke(scan, _lShift || _rShift, altGr, (Native.GetKeyState((int)VK_CAPITAL) & 1) != 0);
        var glyph = layout.Translate(stroke);
        if (glyph is null) { Buffer.Reset(); return; }  // dead key or no text
        Buffer.Printable(glyph, stroke, layout.Id);
    }
}
