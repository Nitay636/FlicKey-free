using System.Text;

namespace FlicKey.Core;

public sealed record ConversionResult(string Text, string TargetLayoutId, IReadOnlyList<Stroke>? Strokes);

// Wrong-layout text fixer. Converts to the NEXT installed layout in a cycle, so
// with two layouts it is "switch to the other one" and with more, repeated
// invocations walk through every layout and back. Two strategies:
//   1. FromTrace: replay the physical keys the user pressed on the layout they
//      were typed under, through the target layout. Exact; needs key history.
//   2. FromText: reverse the text through its source layout to find the physical
//      keys, then translate them. Used for selected text.
public static class LayoutConverter
{
    public static IReadOnlyList<string> SplitGlyphs(string text)
    {
        var list = new List<string>();
        foreach (var rune in text.EnumerateRunes()) list.Add(rune.ToString());
        return list;
    }

    public static ConversionResult? FromTrace(IReadOnlyList<TypingBuffer.Entry> entries,
                                              IReadOnlyList<IKeyboardLayout> layouts)
    {
        if (entries.Count == 0 || layouts.Count < 2) return null;
        var layoutId = entries[0].LayoutId;
        if (layoutId is null) return null;
        var sourceIndex = IndexOf(layouts, layoutId);
        if (sourceIndex < 0) return null;
        var source = layouts[sourceIndex];

        var strokes = new List<Stroke>(entries.Count);
        foreach (var e in entries)
        {
            // History is only trusted when every key came from one layout and
            // still reproduces what is on screen (an IME or dead key would not).
            if (e.Stroke is not Stroke s || e.LayoutId != layoutId || source.Translate(s) != e.Glyph)
                return null;
            strokes.Add(s);
        }

        var target = layouts[(sourceIndex + 1) % layouts.Count];
        var sb = new StringBuilder();
        for (int i = 0; i < entries.Count; i++)
            sb.Append(target.Translate(strokes[i]) ?? entries[i].Glyph);
        return new ConversionResult(sb.ToString(), target.Id, strokes);
    }

    // Direction: characters exclusive to one layout decide the source. Text with
    // none (digits, shared punctuation) is read as the CURRENT layout, so
    // punctuation round-trips instead of cascading.
    public static ConversionResult? FromText(string text, IReadOnlyList<IKeyboardLayout> layouts,
                                             string? currentLayoutId)
    {
        if (layouts.Count < 2 || string.IsNullOrEmpty(text)) return null;
        text = NormalizeQuotes(text);

        var reverse = layouts.Select(BuildReverse).ToList();
        var exclusive = new int[layouts.Count];
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune)) continue;
            var key = rune.ToString();
            int owner = -1, owners = 0;
            for (int i = 0; i < layouts.Count; i++)
                if (reverse[i].ContainsKey(key)) { owner = i; owners++; }
            if (owners == 1) exclusive[owner]++;
        }

        int currentIndex = currentLayoutId is null ? -1 : IndexOf(layouts, currentLayoutId);
        int best = exclusive.Max();
        int sourceIndex;
        if (best > 0)
        {
            var tied = Enumerable.Range(0, layouts.Count).Where(i => exclusive[i] == best).ToList();
            sourceIndex = tied.Contains(currentIndex) ? currentIndex : tied[0];
        }
        else if (currentIndex >= 0) sourceIndex = currentIndex;
        else return null;

        var target = layouts[(sourceIndex + 1) % layouts.Count];
        var sb = new StringBuilder();
        var strokes = new List<Stroke>();
        bool allMapped = true;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = rune.ToString();
            if (reverse[sourceIndex].TryGetValue(glyph, out var stroke))
            {
                var translated = target.Translate(stroke);
                sb.Append(translated ?? glyph);
                strokes.Add(stroke);
            }
            else
            {
                sb.Append(glyph);
                allMapped = false;
            }
        }
        var result = sb.ToString();
        if (result == text) return null;
        return new ConversionResult(result, target.Id, allMapped ? strokes : null);
    }

    // Fold the apostrophe / quote family back to ASCII before reversing: apps
    // that apply "smart quotes" would otherwise leave those characters unmapped.
    public static string NormalizeQuotes(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                '‘' or '’' or '‛' or 'ʼ' or '´' or '′' => '\'',
                '“' or '”' or '″' => '"',
                _ => ch,
            });
        }
        return sb.ToString();
    }

    private static Dictionary<string, Stroke> BuildReverse(IKeyboardLayout layout)
    {
        var map = new Dictionary<string, Stroke>();
        foreach (var stroke in layout.AllStrokes)
        {
            var text = layout.Translate(stroke);
            if (!string.IsNullOrEmpty(text)) map.TryAdd(text, stroke);   // first stroke wins
        }
        return map;
    }

    private static int IndexOf(IReadOnlyList<IKeyboardLayout> layouts, string id)
    {
        for (int i = 0; i < layouts.Count; i++) if (layouts[i].Id == id) return i;
        return -1;
    }
}
