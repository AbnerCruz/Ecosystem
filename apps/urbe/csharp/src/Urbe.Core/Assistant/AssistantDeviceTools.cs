using System.Globalization;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>An event of the device's calendar.</summary>
public sealed record DeviceEvent(string Title, DateTimeOffset Start, DateTimeOffset End, string? Location = null, string? Notes = null, bool AllDay = false);

/// <summary>What a platform does with its own alarm clock, calendar and e-mail app (set by the head: Android, Windows).</summary>
public interface IDevicePlatform
{
    /// <summary>A repeating or one-off alarm in the clock app; <paramref name="days"/> empty = the next time it comes.</summary>
    Task<string> SetAlarmAsync(int hour, int minute, string label, IReadOnlyList<DayOfWeek> days);
    /// <summary>The next alarm set on the device, or null when none is.</summary>
    Task<DateTimeOffset?> NextAlarmAsync();
    /// <summary>The event handed to the calendar app, which shows it for the person to save.</summary>
    Task<string> AddEventAsync(DeviceEvent e);
    /// <summary>The device calendars' events between two times (the system asks the person the first time).</summary>
    Task<IReadOnlyList<DeviceEvent>> EventsAsync(DateTimeOffset from, DateTimeOffset to);
    /// <summary>A message ready in the e-mail app; the person sends it there.</summary>
    Task<string> ComposeEmailAsync(IReadOnlyList<string> to, string subject, string body);
}

/// <summary>
/// The device as the agents see it: the platform plus what the person allowed (Assistente → Aparelho). Each capability
/// is off until switched on, and each write is approved like any other tool's.
/// </summary>
public interface IAssistantDevice : IDevicePlatform
{
    /// <summary>The capabilities the person allowed: <see cref="AssistantDeviceTools.Alarm"/>, Calendar, Email.</summary>
    IReadOnlySet<string> Allowed { get; }
}

/// <summary>A platform with the person's allowances.</summary>
public sealed class AllowedDevice(IDevicePlatform platform, IReadOnlySet<string> allowed) : IAssistantDevice
{
    public IReadOnlySet<string> Allowed => allowed;
    public Task<string> SetAlarmAsync(int hour, int minute, string label, IReadOnlyList<DayOfWeek> days) => platform.SetAlarmAsync(hour, minute, label, days);
    public Task<DateTimeOffset?> NextAlarmAsync() => platform.NextAlarmAsync();
    public Task<string> AddEventAsync(DeviceEvent e) => platform.AddEventAsync(e);
    public Task<IReadOnlyList<DeviceEvent>> EventsAsync(DateTimeOffset from, DateTimeOffset to) => platform.EventsAsync(from, to);
    public Task<string> ComposeEmailAsync(IReadOnlyList<string> to, string subject, string body) => platform.ComposeEmailAsync(to, subject, body);
}

/// <summary>
/// The Assistente's tools for the device: set an alarm, see the next alarm, add and list calendar events, prepare an
/// e-mail. Off until the person allows each one; nothing is sent or saved without the person's approval and the app's
/// own confirmation screen (the e-mail is sent by the person in the e-mail app).
/// </summary>
public static class AssistantDeviceTools
{
    public const string Alarm = "alarme", Calendar = "agenda", Email = "email";

    private static string Str(JsonObject i, string k) => i[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : "";

    private static IAssistantDevice Device(IAssistantHost host, string capability, string what)
    {
        var device = host.Device ?? throw new AiToolException(what + " não está disponível neste aparelho.");
        if (!device.Allowed.Contains(capability))
            throw new AiToolException(what + " está desligado. A pessoa liga em Assistente → Aparelho; não insista nem contorne.");
        return device;
    }

    /// <summary>"07:30" or "7h30" → (7, 30).</summary>
    public static (int Hour, int Minute) ParseTime(string text)
    {
        var parts = text.Trim().ToLowerInvariant().Replace('h', ':').TrimEnd(':').Split(':');
        if (parts.Length is < 1 or > 2 || !int.TryParse(parts[0], out var h) || h is < 0 or > 23)
            throw new AiToolException("Hora inválida: " + text + ". Use HH:MM, por exemplo 07:30.");
        int m = 0;
        if (parts.Length == 2 && parts[1].Length > 0 && (!int.TryParse(parts[1], out m) || m is < 0 or > 59))
            throw new AiToolException("Minuto inválido: " + text + ".");
        return (h, m);
    }

    private static readonly string[] DayNames = ["dom", "seg", "ter", "qua", "qui", "sex", "sab"];

    public static IReadOnlyList<DayOfWeek> ParseDays(JsonNode? node)
    {
        if (node is not JsonArray days) return [];
        var result = new List<DayOfWeek>();
        foreach (var d in days)
        {
            var name = (d is JsonValue v && v.TryGetValue<string>(out var s) ? s : "").Trim().ToLowerInvariant().Replace("á", "a");
            int index = Array.FindIndex(DayNames, n => name.StartsWith(n, StringComparison.Ordinal));
            if (index < 0) throw new AiToolException("Dia inválido: " + name + ". Use dom, seg, ter, qua, qui, sex ou sab.");
            if (!result.Contains((DayOfWeek)index)) result.Add((DayOfWeek)index);
        }
        return result;
    }

    /// <summary>An ISO date-time; without an offset it is the device's local time.</summary>
    public static DateTimeOffset ParseWhen(string text, string field)
    {
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var when)) return when;
        throw new AiToolException(field + " inválido: \"" + text + "\". Use data e hora ISO, por exemplo 2026-11-20T14:00.");
    }

    private static string Clock(int h, int m) => h.ToString("00", CultureInfo.InvariantCulture) + ":" + m.ToString("00", CultureInfo.InvariantCulture);
    private static string Days(IReadOnlyList<DayOfWeek> days) => days.Count == 0 ? "uma vez" : string.Join(", ", days.Select(d => DayNames[(int)d]));
    private static string When(DateTimeOffset t) => t.ToLocalTime().ToString("ddd dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));

    private static JsonObject Schema(string json) => JsonNode.Parse(json)!.AsObject();

    public static AiTool SetAlarm { get; } = new()
    {
        Name = "set_alarm", Access = "write",
        Description = "Marca um despertador no relógio do aparelho (hora, nome e dias da semana opcionais). Só funciona se a pessoa ligou Alarme em Assistente → Aparelho.",
        Parameters = Schema("""{"type":"object","properties":{"time":{"type":"string","description":"HH:MM, 24 h"},"label":{"type":"string"},"days":{"type":"array","items":{"type":"string","enum":["dom","seg","ter","qua","qui","sex","sab"]},"description":"Vazio = só a próxima vez"}},"required":["time"],"additionalProperties":false}"""),
        Label = i => "Despertador às " + Str(i, "time"),
        Preview = (i, _) =>
        {
            var (h, m) = ParseTime(Str(i, "time"));
            return new AiPreview("Despertador", null, null, "Marcar despertador às " + Clock(h, m) + " (" + Days(ParseDays(i["days"])) + ")" + (Str(i, "label") is { Length: > 0 } l ? ": " + l : "") + ". O relógio do aparelho mostra o alarme.");
        },
        Run = async (i, host) =>
        {
            var device = Device(host, Alarm, "O despertador");
            var (h, m) = ParseTime(Str(i, "time"));
            var days = ParseDays(i["days"]);
            return new AiToolOutput(await device.SetAlarmAsync(h, m, Str(i, "label"), days));
        }
    };

    public static AiTool NextAlarm { get; } = new()
    {
        Name = "next_alarm", Access = "read",
        Description = "Diz o próximo despertador marcado no aparelho (o sistema só informa o próximo, não a lista).",
        Parameters = Schema("""{"type":"object","properties":{},"additionalProperties":false}"""),
        Label = _ => "Consultando o despertador",
        Run = async (_, host) =>
        {
            var next = await Device(host, Alarm, "O despertador").NextAlarmAsync();
            return new AiToolOutput(next is { } t ? "Próximo despertador: " + When(t) + "." : "Nenhum despertador marcado no aparelho.");
        }
    };

    public static AiTool AddEvent { get; } = new()
    {
        Name = "add_calendar_event", Access = "write",
        Description = "Cria um evento na agenda do aparelho (título, início, fim, local, notas); o app de agenda mostra para a pessoa salvar. Datas ISO no horário local.",
        Parameters = Schema("""{"type":"object","properties":{"title":{"type":"string"},"start":{"type":"string","description":"ISO, ex.: 2026-11-20T14:00"},"end":{"type":"string","description":"ISO; padrão: 1 hora depois"},"location":{"type":"string"},"notes":{"type":"string"},"all_day":{"type":"boolean"}},"required":["title","start"],"additionalProperties":false}"""),
        Label = i => "Evento: " + Str(i, "title"),
        Preview = (i, _) =>
        {
            var e = Event(i);
            return new AiPreview("Agenda", null, null, "Criar na agenda: " + e.Title + " — " + When(e.Start) + " até " + When(e.End) + (e.Location is { } l ? " · " + l : "") + ".");
        },
        Run = async (i, host) => new AiToolOutput(await Device(host, Calendar, "A agenda").AddEventAsync(Event(i)))
    };

    private static DeviceEvent Event(JsonObject i)
    {
        var title = Str(i, "title");
        if (title.Length == 0) throw new AiToolException("Informe o título do evento.");
        var start = ParseWhen(Str(i, "start"), "Início");
        var end = Str(i, "end") is { Length: > 0 } e ? ParseWhen(e, "Fim") : start.AddHours(1);
        if (end < start) throw new AiToolException("O fim vem antes do início.");
        return new DeviceEvent(title, start, end, Str(i, "location") is { Length: > 0 } l ? l : null, Str(i, "notes") is { Length: > 0 } n ? n : null,
            i["all_day"] is JsonValue a && a.TryGetValue<bool>(out var all) && all);
    }

    public static AiTool ListEvents { get; } = new()
    {
        Name = "list_calendar_events", Access = "read",
        Description = "Lista os eventos da agenda do aparelho num período (padrão: hoje e os próximos 7 dias).",
        Parameters = Schema("""{"type":"object","properties":{"from":{"type":"string"},"to":{"type":"string"}},"additionalProperties":false}"""),
        Label = _ => "Consultando a agenda",
        Run = async (i, host) =>
        {
            var device = Device(host, Calendar, "A agenda");
            var from = Str(i, "from") is { Length: > 0 } f ? ParseWhen(f, "Início") : new DateTimeOffset(DateTime.Today);
            var to = Str(i, "to") is { Length: > 0 } t ? ParseWhen(t, "Fim") : from.AddDays(7);
            if (to < from) throw new AiToolException("O fim do período vem antes do início.");
            if (to - from > TimeSpan.FromDays(366)) throw new AiToolException("Peça no máximo um ano por vez.");
            var events = (await device.EventsAsync(from, to)).OrderBy(e => e.Start).ToList();
            if (events.Count == 0) return new AiToolOutput("Nenhum evento entre " + When(from) + " e " + When(to) + ".");
            return new AiToolOutput(events.Count + (events.Count == 1 ? " evento:\n" : " eventos:\n") + string.Join("\n", events.Take(200).Select(e =>
                "- " + (e.AllDay ? e.Start.ToString("ddd dd/MM", CultureInfo.GetCultureInfo("pt-BR")) + " (dia todo)" : When(e.Start) + "–" + e.End.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)) +
                ": " + e.Title + (e.Location is { } l ? " · " + l : ""))) + (events.Count > 200 ? "\n… e mais " + (events.Count - 200) + "." : ""));
        }
    };

    public static AiTool ComposeEmail { get; } = new()
    {
        Name = "compose_email", Access = "write",
        Description = "Prepara um e-mail no app de e-mail do aparelho (destinatários, assunto, texto). Quem envia é a pessoa, no app de e-mail; o Numa não lê a caixa de entrada.",
        Parameters = Schema("""{"type":"object","properties":{"to":{"type":"array","items":{"type":"string"}},"subject":{"type":"string"},"body":{"type":"string"}},"required":["to","subject","body"],"additionalProperties":false}"""),
        Label = i => "E-mail: " + Str(i, "subject"),
        Preview = (i, _) => new AiPreview("E-mail", null, Str(i, "body"), "Preparar e-mail para " + string.Join(", ", Recipients(i)) + " — " + Str(i, "subject") + ". Você envia no app de e-mail."),
        Run = async (i, host) => new AiToolOutput(await Device(host, Email, "O e-mail").ComposeEmailAsync(Recipients(i), Str(i, "subject"), Str(i, "body")))
    };

    private static IReadOnlyList<string> Recipients(JsonObject i)
    {
        var to = (i["to"] as JsonArray ?? []).Select(x => x is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : "").Where(s => s.Length > 0).ToList();
        if (to.Count == 0) throw new AiToolException("Informe ao menos um destinatário.");
        foreach (var a in to)
            if (!System.Net.Mail.MailAddress.TryCreate(a, out var parsed) || parsed.Address != a)
                throw new AiToolException("Endereço de e-mail inválido: " + a + ".");
        return to;
    }

    public static IReadOnlyList<AiTool> All { get; } = [SetAlarm, NextAlarm, AddEvent, ListEvents, ComposeEmail];

    /// <summary>The mailto: link for a message (RFC 6068), used by hosts without a native composer.</summary>
    public static string MailTo(IReadOnlyList<string> to, string subject, string body) =>
        "mailto:" + string.Join(",", to.Select(Uri.EscapeDataString)) + "?subject=" + Uri.EscapeDataString(subject) + "&body=" + Uri.EscapeDataString(body.Replace("\r\n", "\n").Replace("\n", "\r\n"));

    /// <summary>The event as an iCalendar file (RFC 5545), for hosts whose calendar app opens .ics.</summary>
    public static string Ics(DeviceEvent e)
    {
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");
        static string Utc(DateTimeOffset t) => t.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Urbe//Assistente//PT", "BEGIN:VEVENT",
            "UID:" + Guid.NewGuid().ToString("N") + "@urbe", "DTSTAMP:" + Utc(DateTimeOffset.UtcNow) };
        if (e.AllDay) { lines.Add("DTSTART;VALUE=DATE:" + e.Start.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); lines.Add("DTEND;VALUE=DATE:" + e.End.AddDays(e.End.Date == e.Start.Date ? 1 : 0).ToString("yyyyMMdd", CultureInfo.InvariantCulture)); }
        else { lines.Add("DTSTART:" + Utc(e.Start)); lines.Add("DTEND:" + Utc(e.End)); }
        lines.Add("SUMMARY:" + Esc(e.Title));
        if (e.Location is { } l) lines.Add("LOCATION:" + Esc(l));
        if (e.Notes is { } n) lines.Add("DESCRIPTION:" + Esc(n));
        lines.AddRange(["END:VEVENT", "END:VCALENDAR"]);
        return string.Join("\r\n", lines) + "\r\n";
    }
}
