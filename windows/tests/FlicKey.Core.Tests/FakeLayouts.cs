using FlicKey.Core;

namespace FlicKey.Core.Tests;

// Minimal US and Hebrew layouts keyed by scan code, enough to exercise the
// converter without Windows.
public sealed class FakeLayout : IKeyboardLayout
{
    private readonly Dictionary<int, (string Plain, string Shifted)> _keys;
    public string Id { get; }

    public FakeLayout(string id, Dictionary<int, (string, string)> keys) { Id = id; _keys = keys; }

    public string? Translate(Stroke s)
    {
        if (!_keys.TryGetValue(s.ScanCode, out var k)) return null;
        var shifted = s.Shift ^ s.CapsLock;
        return shifted ? k.Shifted : k.Plain;
    }

    public IEnumerable<Stroke> AllStrokes =>
        _keys.Keys.SelectMany(sc => new[] { new Stroke(sc, false, false, false), new Stroke(sc, true, false, false) });

    private static void Row(Dictionary<int, (string, string)> d, int start, string plain, string shifted)
    {
        for (int i = 0; i < plain.Length; i++) d[start + i] = (plain[i].ToString(), shifted[i].ToString());
    }

    public static FakeLayout Us()
    {
        var d = new Dictionary<int, (string, string)>();
        Row(d, 0x10, "qwertyuiop", "QWERTYUIOP");
        Row(d, 0x1E, "asdfghjkl;'", "ASDFGHJKL:\"");
        Row(d, 0x2C, "zxcvbnm,./", "ZXCVBNM<>?");
        d[0x29] = ("`", "~");
        d[0x39] = (" ", " ");
        return new FakeLayout("US", d);
    }

    public static FakeLayout Hebrew()
    {
        var d = new Dictionary<int, (string, string)>();
        Row(d, 0x10, "/'קראטוןםפ", "/'קראטוןםפ");
        Row(d, 0x1E, "שדגכעיחלךף,", "שדגכעיחלךף,");
        Row(d, 0x2C, "זסבהנמצתץ.", "זסבהנמצתץ.");
        d[0x29] = (";", "~");
        d[0x39] = (" ", " ");
        return new FakeLayout("HE", d);
    }
}
