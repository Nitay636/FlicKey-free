namespace FlicKey.Core;

// The run of characters the user has just typed into the current field. A
// boundary (caret moved, shortcut, app switch) clears it, so it only ever holds
// characters the user actually typed and the synthetic backspaces used by a fix
// can never over-delete.
public sealed class TypingBuffer
{
    public readonly record struct Entry(string Glyph, Stroke? Stroke, string? LayoutId);

    private readonly List<Entry> _entries = new();
    private readonly int _maxLength;

    public TypingBuffer(int maxLength = 120) { _maxLength = maxLength; }

    public IReadOnlyList<Entry> Entries => _entries;
    public bool IsEmpty => _entries.Count == 0;
    public string Text => string.Concat(_entries.Select(e => e.Glyph));

    // Number of Backspace presses that remove the whole run (one per glyph; a
    // glyph can be several UTF-16 units but is still one key press to delete).
    public int Count => _entries.Count;

    public void Printable(string glyph, Stroke? stroke, string? layoutId)
    {
        _entries.Add(new Entry(glyph, stroke, layoutId));
        if (_entries.Count > _maxLength) _entries.RemoveRange(0, _entries.Count - _maxLength);
    }

    public void Backspace()
    {
        if (_entries.Count > 0) _entries.RemoveAt(_entries.Count - 1);
    }

    public void Reset() => _entries.Clear();

    // Replace the run with converted text, keeping the strokes so an immediate
    // re-trigger cycles to the next layout instead of guessing from the text.
    public void Adopt(string convertedText, string targetLayoutId, IReadOnlyList<Stroke>? strokes)
    {
        _entries.Clear();
        var glyphs = LayoutConverter.SplitGlyphs(convertedText);
        for (int i = 0; i < glyphs.Count; i++)
        {
            Stroke? s = strokes is not null && strokes.Count == glyphs.Count ? strokes[i] : null;
            _entries.Add(new Entry(glyphs[i], s, s is null ? null : targetLayoutId));
        }
    }
}
