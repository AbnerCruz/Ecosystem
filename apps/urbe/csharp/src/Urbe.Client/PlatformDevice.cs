using Avalonia.Platform.Storage;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// The device's alarm clock, calendar and e-mail app for the Assistente. A head sets <see cref="Current"/> (Android: the
/// clock, the calendar provider, the e-mail app); elsewhere the system's own apps are opened through the launcher.
/// </summary>
public static class PlatformDevice
{
    public static IDevicePlatform? Current { get; set; }
}

/// <summary>
/// A computer: the e-mail app by a mailto: link and the calendar app by an .ics file it opens. There is no clock app
/// to set or read alarms, nor a calendar to read, so those say so instead of pretending.
/// </summary>
public sealed class LauncherDevice(Func<ILauncher?> launcher) : IDevicePlatform
{
    private ILauncher Launcher => launcher() ?? throw new AiToolException("Este aparelho não deixou abrir outros apps.");

    public Task<string> SetAlarmAsync(int hour, int minute, string label, IReadOnlyList<DayOfWeek> days) =>
        throw new AiToolException("Este computador não tem um relógio com despertador que o Urbe consiga marcar. No celular, o despertador funciona.");

    public Task<DateTimeOffset?> NextAlarmAsync() =>
        throw new AiToolException("Este computador não informa despertadores ao Urbe.");

    public async Task<string> AddEventAsync(DeviceEvent e)
    {
        var file = Path.Combine(Path.GetTempPath(), "urbe-evento-" + Guid.NewGuid().ToString("N")[..8] + ".ics");
        await File.WriteAllTextAsync(file, AssistantDeviceTools.Ics(e));
        if (!await Launcher.LaunchFileInfoAsync(new FileInfo(file)))
            throw new AiToolException("Nenhum app de agenda abriu o evento.");
        return "O evento “" + e.Title + "” foi aberto no app de agenda; ele fica salvo quando a pessoa confirmar lá.";
    }

    public Task<IReadOnlyList<DeviceEvent>> EventsAsync(DateTimeOffset from, DateTimeOffset to) =>
        throw new AiToolException("Este computador não dá ao Urbe acesso à agenda. No celular, a consulta funciona.");

    public async Task<string> ComposeEmailAsync(IReadOnlyList<string> to, string subject, string body)
    {
        if (!await Launcher.LaunchUriAsync(new Uri(AssistantDeviceTools.MailTo(to, subject, body))))
            throw new AiToolException("Nenhum app de e-mail abriu a mensagem.");
        return "A mensagem está pronta no app de e-mail; a pessoa revisa e envia lá. Nada foi enviado pelo Urbe.";
    }
}
