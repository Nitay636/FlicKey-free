namespace FlicKey.Core;

// One physical key press, independent of any layout. ScanCode identifies the
// physical key (virtual-key codes differ between layouts, scan codes do not).
public readonly record struct Stroke(int ScanCode, bool Shift, bool AltGr, bool CapsLock);

// A keyboard layout as the converter needs it: translate a physical stroke to
// the text it produces, and list every stroke that produces text so the layout
// can be reversed (text -> stroke) when no key history exists.
public interface IKeyboardLayout
{
    string Id { get; }

    // Text produced by the stroke on this layout, or null when it produces none
    // (dead key, no mapping).
    string? Translate(Stroke stroke);

    // Every stroke the layout can type, in preference order for reverse lookup
    // (unshifted before shifted).
    IEnumerable<Stroke> AllStrokes { get; }
}
