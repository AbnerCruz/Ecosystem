namespace Urbe.Client;

/// <summary>Configurações → Diagnóstico de toques (settings.js + touch-debug.js): on or off on this device.</summary>
public sealed partial class MainView
{
    private const string TouchDebugKey = "urbe.touchDebug";
    private TouchDebug? _touchDebug;

    /// <summary>The diagnostic overlay, over everything (the dialogs too), made the first time it is turned on.</summary>
    public TouchDebug TouchDebug => _touchDebug ??= MakeTouchDebug();

    private TouchDebug MakeTouchDebug()
    {
        var t = new TouchDebug(this) { Version = "nativo " + (typeof(MainView).Assembly.GetName().Version?.ToString(3) ?? "") };
        t.TurnedOff += () => SetTouchDebug(false);
        Editor.ScrollObserved += t.RecordScroll;
        SetRowSpan(t, 2);
        SetColumnSpan(t, 2);
        Children.Add(t);
        return t;
    }

    /// <summary>dbg.isOn().</summary>
    public bool TouchDebugOn => _touchDebug?.IsVisible == true;

    /// <summary>dbg.set(v): kept on this device (localStorage urbe.touchDebug).</summary>
    public void SetTouchDebug(bool on)
    {
        Customization.Device.Set(TouchDebugKey, on ? "1" : null);
        if (on) TouchDebug.IsVisible = true;
        else if (_touchDebug is not null) _touchDebug.IsVisible = false;
    }

    /// <summary>The settings entry: turns it on (and says how it works) or off.</summary>
    private async Task ToggleTouchDebugAsync()
    {
        var on = !TouchDebugOn;
        SetTouchDebug(on);
        if (on)
            await Dialogs.AlertAsync("Diagnóstico ligado",
                "Toque, digite, abra e feche o teclado. O painel registra foco, janela, IME, rolagem e toques.\n\nUse “Copiar relatório” e cole o texto na conversa. Para desligar, toque no ×.");
    }

    /// <summary>On at start when it was on (localStorage.getItem(KEY) === '1').</summary>
    private void RestoreTouchDebug()
    {
        if (Customization.Device.Get(TouchDebugKey) == "1") SetTouchDebug(true);
    }
}
