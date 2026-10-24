namespace Urbe.Client;

/// <summary>Android opens the IME only after the editable area is touched. Desktop retains keyboard-first focus.</summary>
public static class KeyboardFocusPolicy
{
    public static bool AutoFocusOnOpen(bool isAndroid) => !isAndroid;
    public static bool FocusEditableOnTouch(bool isAndroid, bool editableTouched) => !isAndroid || editableTouched;
}
