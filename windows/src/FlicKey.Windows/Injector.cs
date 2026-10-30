namespace FlicKey.Windows;

// Synthetic keyboard input. Everything sent here carries the injected flag, which
// KeyboardMonitor ignores, so a fix never feeds back into the typing buffer.
internal static class Injector
{
    private const ushort VK_BACK = 0x08, VK_CONTROL = 0x11;

    public static void Backspaces(int count)
    {
        if (count <= 0) return;
        var inputs = new List<Native.INPUT>(count * 2);
        for (int i = 0; i < count; i++)
        {
            inputs.Add(Key(VK_BACK, 0, 0));
            inputs.Add(Key(VK_BACK, 0, Native.KEYEVENTF_KEYUP));
        }
        Send(inputs);
    }

    public static void TypeText(string text)
    {
        var inputs = new List<Native.INPUT>(text.Length * 2);
        foreach (var ch in text)
        {
            inputs.Add(Key(0, ch, Native.KEYEVENTF_UNICODE));
            inputs.Add(Key(0, ch, Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP));
        }
        Send(inputs);
    }

    public static void CtrlCombo(ushort vk)
    {
        Send(new List<Native.INPUT>
        {
            Key(VK_CONTROL, 0, 0),
            Key(vk, 0, 0),
            Key(vk, 0, Native.KEYEVENTF_KEYUP),
            Key(VK_CONTROL, 0, Native.KEYEVENTF_KEYUP),
        });
    }

    private static Native.INPUT Key(ushort vk, ushort scan, uint flags) => new()
    {
        type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags } },
    };

    private static void Send(List<Native.INPUT> inputs)
    {
        if (inputs.Count == 0) return;
        Native.SendInput((uint)inputs.Count, inputs.ToArray(), System.Runtime.InteropServices.Marshal.SizeOf<Native.INPUT>());
    }
}
