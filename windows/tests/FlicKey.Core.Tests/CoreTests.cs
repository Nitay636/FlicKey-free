using FlicKey.Core;
using Xunit;

namespace FlicKey.Core.Tests;

public class DoubleTapTests
{
    [Fact]
    public void TwoQuickCleanTapsFire()
    {
        var m = new DoubleTapStateMachine();
        m.ShiftDown(false, 0.00); Assert.False(m.ShiftUp(false, 0.05));
        m.ShiftDown(false, 0.15); Assert.True(m.ShiftUp(false, 0.20));
    }

    [Fact]
    public void SlowSecondTapDoesNotFire()
    {
        var m = new DoubleTapStateMachine();
        m.ShiftDown(false, 0.00); m.ShiftUp(false, 0.05);
        m.ShiftDown(false, 0.60); Assert.False(m.ShiftUp(false, 0.65));
    }

    [Fact]
    public void ShiftPlusLetterBreaksThePair()
    {
        var m = new DoubleTapStateMachine();
        m.ShiftDown(false, 0.00); m.ShiftUp(false, 0.05);
        m.ShiftDown(false, 0.10); m.KeyPressed(); Assert.False(m.ShiftUp(false, 0.15));
        m.ShiftDown(false, 0.20); Assert.False(m.ShiftUp(false, 0.25));   // first tap again, not a pair
    }

    [Fact]
    public void LongHoldIsNotATap()
    {
        var m = new DoubleTapStateMachine();
        m.ShiftDown(false, 0.00); Assert.False(m.ShiftUp(false, 0.80));
        m.ShiftDown(false, 0.85); Assert.False(m.ShiftUp(false, 0.90));
    }

    [Fact]
    public void OtherModifierHeldBlocksTap()
    {
        var m = new DoubleTapStateMachine();
        m.ShiftDown(true, 0.00); Assert.False(m.ShiftUp(true, 0.05));
    }
}

public class TypingBufferTests
{
    [Fact]
    public void TracksTypingAndBackspace()
    {
        var b = new TypingBuffer();
        b.Printable("a", new Stroke(0x1E, false, false, false), "US");
        b.Printable("b", new Stroke(0x30, false, false, false), "US");
        b.Backspace();
        Assert.Equal("a", b.Text);
        b.Reset();
        Assert.True(b.IsEmpty);
    }

    [Fact]
    public void CapsAtMaxLength()
    {
        var b = new TypingBuffer(3);
        foreach (var c in "abcde") b.Printable(c.ToString(), null, null);
        Assert.Equal("cde", b.Text);
    }
}

public class ConverterTests
{
    private static readonly IReadOnlyList<IKeyboardLayout> Layouts =
        new IKeyboardLayout[] { FakeLayout.Us(), FakeLayout.Hebrew() };

    private static TypingBuffer Type(IKeyboardLayout layout, string keysAsUsText)
    {
        var us = FakeLayout.Us();
        var buffer = new TypingBuffer();
        foreach (var ch in keysAsUsText)
        {
            var stroke = us.AllStrokes.First(s => us.Translate(s) == ch.ToString());
            buffer.Printable(layout.Translate(stroke)!, stroke, layout.Id);
        }
        return buffer;
    }

    [Fact]
    public void TraceConvertsEnglishTypingToHebrew()
    {
        var buffer = Type(Layouts[0], "akuo");
        var result = LayoutConverter.FromTrace(buffer.Entries, Layouts);
        Assert.Equal("שלום", result!.Text);
        Assert.Equal("HE", result.TargetLayoutId);
    }

    [Fact]
    public void TraceKeepsCapitalsAndSpaces()
    {
        var buffer = Type(Layouts[0], "Akuo vnmc");
        Assert.Equal("שלום המצב", LayoutConverter.FromTrace(buffer.Entries, Layouts)!.Text);
    }

    [Fact]
    public void TraceReTriggerCyclesBack()
    {
        var buffer = Type(Layouts[0], "akuo");
        var first = LayoutConverter.FromTrace(buffer.Entries, Layouts)!;
        buffer.Adopt(first.Text, first.TargetLayoutId, first.Strokes);
        var second = LayoutConverter.FromTrace(buffer.Entries, Layouts)!;
        Assert.Equal("akuo", second.Text);
        Assert.Equal("US", second.TargetLayoutId);
    }

    [Fact]
    public void TraceRefusesMixedLayoutHistory()
    {
        var buffer = Type(Layouts[0], "ak");
        var us = FakeLayout.Us();
        var stroke = us.AllStrokes.First(s => us.Translate(s) == "u");
        buffer.Printable("ו", stroke, "HE");
        Assert.Null(LayoutConverter.FromTrace(buffer.Entries, Layouts));
    }

    [Fact]
    public void TraceRefusesWhenGlyphDoesNotMatchLayout()
    {
        var buffer = new TypingBuffer();
        buffer.Printable("x", new Stroke(0x1E, false, false, false), "US");   // 'x' is not what the key makes
        Assert.Null(LayoutConverter.FromTrace(buffer.Entries, Layouts));
    }

    [Fact]
    public void TextConvertsEnglishToHebrewAndBack()
    {
        Assert.Equal("שלום", LayoutConverter.FromText("akuo", Layouts, "US")!.Text);
        Assert.Equal("akuo", LayoutConverter.FromText("שלום", Layouts, "HE")!.Text);
    }

    [Fact]
    public void TextWithOnlySharedPunctuationFollowsCurrentLayout()
    {
        // ';' on US is the Hebrew 'ף' key; '.' is shared by both in this fixture.
        var fromUs = LayoutConverter.FromText("w", Layouts, "US")!;
        Assert.Equal("'", fromUs.Text);
        Assert.Equal("HE", fromUs.TargetLayoutId);
        Assert.Equal("w", LayoutConverter.FromText("'", Layouts, "HE")!.Text);
    }

    [Fact]
    public void SmartQuotesFoldBeforeConverting()
    {
        // Hebrew 'w' key types an apostrophe; WhatsApp-style apps turn it into U+2019.
        Assert.Equal("w", LayoutConverter.FromText("’", Layouts, "HE")!.Text);
    }

    [Fact]
    public void ThreeLayoutsCycleInOrder()
    {
        var third = new FakeLayout("XX", new Dictionary<int, (string, string)> { [0x1E] = ("1", "!"), [0x1F] = ("2", "@") });
        var all = new IKeyboardLayout[] { Layouts[0], Layouts[1], third };
        Assert.Equal("XX", LayoutConverter.FromText("ש", all, "HE")!.TargetLayoutId);
    }

    [Fact]
    public void NoOpWhenNothingChanges()
    {
        Assert.Null(LayoutConverter.FromText("123", Layouts, "US"));
        Assert.Null(LayoutConverter.FromText("", Layouts, "US"));
    }

    [Fact]
    public void OneLayoutCannotConvert()
    {
        Assert.Null(LayoutConverter.FromText("akuo", new[] { Layouts[0] }, "US"));
    }
}
