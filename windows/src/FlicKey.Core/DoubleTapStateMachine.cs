namespace FlicKey.Core;

// Pure decision logic for "the user double-tapped Shift". Feed it transitions in
// order; ShiftUp returns true on the release that completes a valid double-tap.
//
// A valid tap = Shift pressed then released with (a) no intervening non-modifier
// key, (b) no other modifier held, (c) a short press duration. Two such taps
// within Window fire the trigger. Any non-clean release clears the pending first
// tap, so "Shift+A ... pause ... Shift" can never pair into a false trigger.
public sealed class DoubleTapStateMachine
{
    public double Window { get; init; } = 0.30;          // max gap between the two taps (s)
    public double MaxTapDuration { get; init; } = 0.30;  // a tap must be a quick press -> release (s)

    private double? _shiftDownAt;
    private bool _interveningKey;
    private double? _lastTapAt;

    public void ShiftDown(bool otherMods, double time)
    {
        if (otherMods) { Reset(); return; }
        _shiftDownAt = time;
        _interveningKey = false;
    }

    // A non-modifier key was pressed: Shift is acting as a modifier, not a tap.
    public void KeyPressed()
    {
        _interveningKey = true;
        _lastTapAt = null;
    }

    // Another modifier (Ctrl/Alt/Win) became active: this Shift isn't a clean tap.
    public void OtherModifierPressed()
    {
        _interveningKey = true;
        _lastTapAt = null;
    }

    public bool ShiftUp(bool otherMods, double time)
    {
        var down = _shiftDownAt;
        _shiftDownAt = null;
        if (down is null || _interveningKey || otherMods || time - down.Value > MaxTapDuration)
        {
            _lastTapAt = null;       // non-clean release breaks any pending pair
            return false;
        }
        if (_lastTapAt is double last && time - last <= Window)
        {
            _lastTapAt = null;
            return true;             // second clean tap in time -> fire
        }
        _lastTapAt = time;           // first clean tap; wait for the second
        return false;
    }

    private void Reset()
    {
        _shiftDownAt = null;
        _interveningKey = false;
        _lastTapAt = null;
    }
}
