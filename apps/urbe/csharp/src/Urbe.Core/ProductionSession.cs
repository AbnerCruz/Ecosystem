namespace Urbe.Core;

/// <summary>
/// AbnerCruz/Urbe#51 (owner, 19/11/2026): the last note and the last conversation come back only while the person is in
/// the same production session — switching screens, minimizing, a short interruption. After a long pause the Urbe opens
/// on the Área de trabalho. Nothing is lost either way: notes, drafts and conversations stay saved. The owner set no
/// length for the pause; <see cref="DefaultPause"/> is the starting point and the person chooses another in Configurações.
/// </summary>
public static class ProductionSession
{
    public static readonly TimeSpan DefaultPause = TimeSpan.FromMinutes(30);

    /// <summary>What Configurações offers (label, pause; null = always reopen where it was).</summary>
    public static readonly (string Label, TimeSpan? Pause)[] Choices =
    [
        ("5 minutos", TimeSpan.FromMinutes(5)), ("30 minutos", TimeSpan.FromMinutes(30)), ("2 horas", TimeSpan.FromHours(2)),
        ("1 dia", TimeSpan.FromDays(1)), ("Sempre reabrir onde parei", null)
    ];

    /// <summary>
    /// True when the session that ended at <paramref name="lastActive"/> goes on at <paramref name="now"/>. A clock that
    /// went backwards (a changed time zone, a corrected clock) counts as a new session, never as a reason to reopen.
    /// </summary>
    public static bool Continues(DateTimeOffset? lastActive, DateTimeOffset now, TimeSpan? pause) =>
        pause is null || lastActive is { } last && now >= last && now - last <= pause.Value;
}
