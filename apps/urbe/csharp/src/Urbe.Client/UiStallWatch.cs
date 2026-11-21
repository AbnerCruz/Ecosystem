using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;

namespace Urbe.Client;

/// <summary>
/// CST-011: notices when the interface stops answering and writes it to the Relatório de erros with what the app was
/// doing — never the text of a note. A beat every half second on the interface's thread; a beat that comes late by
/// more than <see cref="Threshold"/> (Android vitals' "frozen frame", 700 ms) is a stall. The person sees nothing.
/// </summary>
public static class UiStallWatch
{
    public static readonly TimeSpan Beat = TimeSpan.FromMilliseconds(500), Threshold = TimeSpan.FromMilliseconds(700);
    private static readonly Stopwatch Clock = new();
    private static DispatcherTimer? _timer;
    private static volatile string _doing = "uso geral";

    /// <summary>The stalls seen since the app opened: how long and during what (tests, the report).</summary>
    public static List<(TimeSpan Length, string Doing)> Seen { get; } = [];

    /// <summary>Names what the app starts doing now (a short label, never the person's text).</summary>
    public static void Doing(string what) => _doing = what;

    public static void Start()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer(Beat, DispatcherPriority.Background, (_, _) => Check(Clock.Elapsed));
        Clock.Restart();
        _timer.Start();
    }

    /// <summary>The app left the screen (Android pause): no beat is due until it comes back.</summary>
    public static void Pause() => _timer?.Stop();

    public static void Resume()
    {
        if (_timer is null) return;
        Clock.Restart();
        _timer.Start();
    }

    /// <summary>A beat <paramref name="elapsed"/> after the previous one: late by more than the threshold is a stall.</summary>
    public static TimeSpan? Check(TimeSpan elapsed)
    {
        Clock.Restart();
        var late = elapsed - Beat;
        // a gap of minutes is a computer that slept, not a stall (Android kills a stalled app at 5 s)
        if (late < Threshold || late > TimeSpan.FromMinutes(1)) return null;
        var doing = _doing;
        Seen.Add((late, doing));
        CrashLog.Note("travamento", "A interface ficou parada " + late.TotalSeconds.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) +
            " s durante: " + doing + ".");
        return late;
    }
}
