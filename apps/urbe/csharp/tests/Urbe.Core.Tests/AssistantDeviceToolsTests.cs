using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>The Assistente's alarm clock, calendar and e-mail: off until allowed, approved, honest about the platform.</summary>
public sealed class AssistantDeviceToolsTests
{
    private sealed class Phone : IDevicePlatform
    {
        public List<string> Calls { get; } = [];
        public Task<string> SetAlarmAsync(int hour, int minute, string label, IReadOnlyList<DayOfWeek> days) { Calls.Add($"alarm {hour}:{minute} {label} {string.Join(",", days)}"); return Task.FromResult("ok"); }
        public Task<DateTimeOffset?> NextAlarmAsync() => Task.FromResult<DateTimeOffset?>(new DateTimeOffset(2026, 11, 18, 7, 30, 0, TimeSpan.FromHours(-3)));
        public Task<string> AddEventAsync(DeviceEvent e) { Calls.Add("event " + e.Title + " " + (e.End - e.Start).TotalMinutes); return Task.FromResult("ok"); }
        public Task<IReadOnlyList<DeviceEvent>> EventsAsync(DateTimeOffset from, DateTimeOffset to) => Task.FromResult<IReadOnlyList<DeviceEvent>>(
            [new("Dentista", from.AddHours(10), from.AddHours(11), "Centro"), new("Aniversário", from, from.AddDays(1), AllDay: true)]);
        public Task<string> ComposeEmailAsync(IReadOnlyList<string> to, string subject, string body) { Calls.Add("mail " + string.Join(",", to) + " " + subject); return Task.FromResult("ok"); }
    }

    private sealed class Host(IAssistantDevice? device) : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge => new(Documents);
        public int ReadChars => 2000;
        public IAssistantDevice? Device => device;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static JsonObject J(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public async Task EachCapabilityIsOffUntilThePersonAllowsIt()
    {
        var phone = new Phone();
        var off = new Host(new AllowedDevice(phone, new HashSet<string>()));
        var error = await Assert.ThrowsAsync<AiToolException>(() => AssistantDeviceTools.SetAlarm.Run(J("""{"time":"07:30"}"""), off));
        Assert.Contains("Assistente → Aparelho", error.Message);
        await Assert.ThrowsAsync<AiToolException>(() => AssistantDeviceTools.ListEvents.Run(J("{}"), off));
        await Assert.ThrowsAsync<AiToolException>(() => AssistantDeviceTools.ComposeEmail.Run(J("""{"to":["a@b.com"],"subject":"x","body":"y"}"""), off));
        Assert.Empty(phone.Calls);
        // a host with no device at all says so
        Assert.Contains("não está disponível", (await Assert.ThrowsAsync<AiToolException>(() => AssistantDeviceTools.NextAlarm.Run(J("{}"), new Host(null)))).Message);
        // only the calendar allowed: the calendar works, the e-mail does not
        var calendar = new Host(new AllowedDevice(phone, new HashSet<string> { AssistantDeviceTools.Calendar }));
        Assert.Contains("Dentista", (await AssistantDeviceTools.ListEvents.Run(J("{}"), calendar)).Content);
        await Assert.ThrowsAsync<AiToolException>(() => AssistantDeviceTools.ComposeEmail.Run(J("""{"to":["a@b.com"],"subject":"x","body":"y"}"""), calendar));
    }

    [Fact]
    public async Task AllowedTheyReachTheDeviceWithWhatWasAsked()
    {
        var phone = new Phone();
        var host = new Host(new AllowedDevice(phone, new HashSet<string> { "alarme", "agenda", "email" }));
        Assert.Equal("write", AssistantDeviceTools.SetAlarm.Access);
        Assert.Equal("write", AssistantDeviceTools.AddEvent.Access);
        Assert.Equal("write", AssistantDeviceTools.ComposeEmail.Access);
        Assert.Equal("read", AssistantDeviceTools.ListEvents.Access);
        var alarm = J("""{"time":"6h45","label":"Academia","days":["seg","qua","sex"]}""");
        Assert.Contains("06:45 (seg, qua, sex): Academia", AssistantDeviceTools.SetAlarm.Preview!(alarm, host).Note);
        await AssistantDeviceTools.SetAlarm.Run(alarm, host);
        Assert.Equal("alarm 6:45 Academia Monday,Wednesday,Friday", phone.Calls[^1]);
        Assert.Contains("Próximo despertador", (await AssistantDeviceTools.NextAlarm.Run(J("{}"), host)).Content);
        await AssistantDeviceTools.AddEvent.Run(J("""{"title":"Reunião","start":"2026-11-20T14:00"}"""), host);
        Assert.Equal("event Reunião 60", phone.Calls[^1]);   // one hour by default
        var listed = (await AssistantDeviceTools.ListEvents.Run(J("""{"from":"2026-11-20T00:00","to":"2026-11-21T00:00"}"""), host)).Content;
        Assert.Contains("2 eventos", listed);
        Assert.Contains("(dia todo): Aniversário", listed);
        await AssistantDeviceTools.ComposeEmail.Run(J("""{"to":["ana@exemplo.org"],"subject":"Ata","body":"Segue."}"""), host);
        Assert.Equal("mail ana@exemplo.org Ata", phone.Calls[^1]);
        // bad input never reaches the device
        int calls = phone.Calls.Count;
        await Assert.ThrowsAsync<AiToolException>(() => AssistantDeviceTools.SetAlarm.Run(J("""{"time":"25:00"}"""), host));
        await Assert.ThrowsAsync<AiToolException>(() => AssistantDeviceTools.AddEvent.Run(J("""{"title":"x","start":"2026-11-20T14:00","end":"2026-11-20T13:00"}"""), host));
        await Assert.ThrowsAsync<AiToolException>(() => AssistantDeviceTools.ComposeEmail.Run(J("""{"to":["não é e-mail"],"subject":"x","body":"y"}"""), host));
        Assert.Equal(calls, phone.Calls.Count);
    }

    [Fact]
    public void IcsAndMailtoFollowTheirStandards()
    {
        var ics = AssistantDeviceTools.Ics(new DeviceEvent("Aula; prova, final", new DateTimeOffset(2026, 11, 20, 14, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 11, 20, 15, 0, 0, TimeSpan.FromHours(-3)), "Sala 2", "linha 1\nlinha 2"));
        Assert.Contains("DTSTART:20261120T170000Z\r\n", ics);
        Assert.Contains("SUMMARY:Aula\\; prova\\, final\r\n", ics);
        Assert.Contains("DESCRIPTION:linha 1\\nlinha 2\r\n", ics);
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.Equal("mailto:ana%40x.org?subject=Ol%C3%A1%20%26%20tchau&body=a%0D%0Ab", AssistantDeviceTools.MailTo(["ana@x.org"], "Olá & tchau", "a\nb"));
    }

    [Fact]
    public void ThePermissionsAreSavedAndUnknownOnesIgnored()
    {
        var c = new AiConfig();
        Assert.Empty(c.DeviceAccess);
        c.DeviceAccess.Add("agenda");
        var json = AssistantStore.ConfigToJson(c);
        json["deviceAccess"]!.AsArray().Add("root");
        var back = AssistantStore.ConfigFromJson(json);
        Assert.Equal(["agenda"], back.DeviceAccess);
    }
}
