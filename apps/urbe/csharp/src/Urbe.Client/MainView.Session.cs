using Avalonia.Input;
using Avalonia.Interactivity;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// AbnerCruz/Urbe#51: within the same production session the Urbe comes back where the person was (the note, the
/// conversation); after a long pause (Configurações → "Ao voltar ao Urbe") it opens on the Área de trabalho. A run of the
/// Assistente going on keeps the session alive. Nothing is closed without being saved, and nothing is deleted.
/// </summary>
public sealed partial class MainView
{
    /// <summary>The clock of the session rule (tests set it).</summary>
    public static Func<DateTimeOffset> SessionClock { get; set; } = () => DateTimeOffset.Now;

    /// <summary>False when this opening, or this return from the background, began a new production session.</summary>
    public bool ContinuesSession { get; private set; } = true;

    private DateTimeOffset _lastActiveWritten = DateTimeOffset.MinValue;

    private void WatchActivity()
    {
        AddHandler(PointerPressedEvent, (_, _) => Touch(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, _) => Touch(), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>The person used the app now (written at most once a minute, and at once with <paramref name="force"/>).</summary>
    public void Touch(bool force = false)
    {
        var now = SessionClock();
        if (!force && now - _lastActiveWritten < TimeSpan.FromMinutes(1)) return;
        _lastActiveWritten = now;
        Customization.LastActive = now;
    }

    /// <summary>At an opening of the app: does it continue the last session? Decided once; the clock starts again.</summary>
    private bool OpeningContinuesSession()
    {
        ContinuesSession = ProductionSession.Continues(Customization.LastActive, SessionClock(), Customization.SessionPause);
        Touch(force: true);
        return ContinuesSession;
    }

    bool IAssistantApp.ContinuesSession => ContinuesSession;

    /// <summary>
    /// Back from the background with the app still running: a short pause, or the Assistente working, goes on where it
    /// was; after a long one the note closes (saved) and the Área de trabalho shows, and the Assistente starts a new
    /// conversation next time (the old one stays in Conversas).
    /// </summary>
    public async Task ReturnFromBackgroundAsync()
    {
        if (Assistant.Running || ProductionSession.Continues(Customization.LastActive, SessionClock(), Customization.SessionPause))
        {
            Touch(force: true);
            return;
        }
        ContinuesSession = false;
        Touch(force: true);
        if (AssistantOpen) ToggleAssistant();
        Assistant.StartFresh();
        if (Editor.IsVisible) await Editor.CloseToHomeAsync();
        ShowWorkspace();
    }
}
