using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Urbe.Client;
using Urbe.Core.Assistant;

[assembly: UsesPermission("com.android.alarm.permission.SET_ALARM")]
[assembly: UsesPermission(Android.Manifest.Permission.ReadCalendar)]

namespace Urbe.AndroidHost;

/// <summary>
/// The Assistente's device tools on Android: the clock app sets the alarm (the system's own screen confirms it), the
/// next alarm comes from AlarmManager, an event opens in the calendar app to be saved there, the calendars are read
/// through CalendarContract after the person allows it, and an e-mail opens ready in the e-mail app to be sent there.
/// </summary>
internal sealed class AndroidDevice : IDevicePlatform
{
    public static void Register() => PlatformDevice.Current = new AndroidDevice();

    private static Context Ctx => Application.Context;

    private static void Start(Intent intent, string missing)
    {
        intent.AddFlags(ActivityFlags.NewTask);
        try { Ctx.StartActivity(intent); }
        catch (ActivityNotFoundException) { throw new AiToolException(missing); }
    }

    public Task<string> SetAlarmAsync(int hour, int minute, string label, IReadOnlyList<DayOfWeek> days)
    {
        var intent = new Intent(AlarmClock.ActionSetAlarm);
        intent.PutExtra(AlarmClock.ExtraHour, hour);
        intent.PutExtra(AlarmClock.ExtraMinutes, minute);
        if (label.Length > 0) intent.PutExtra(AlarmClock.ExtraMessage, label);
        // Calendar.SUNDAY is 1, as DayOfWeek.Sunday + 1
        if (days.Count > 0) intent.PutIntegerArrayListExtra(AlarmClock.ExtraDays, days.Select(d => Java.Lang.Integer.ValueOf((int)d + 1)!).ToList());
        intent.PutExtra(AlarmClock.ExtraSkipUi, false);
        Start(intent, "Nenhum app de relógio neste aparelho aceita despertadores.");
        return Task.FromResult("O relógio do aparelho abriu com o despertador das " + hour.ToString("00") + ":" + minute.ToString("00") + ". Ele fica marcado como o relógio mostrar.");
    }

    public Task<DateTimeOffset?> NextAlarmAsync()
    {
        var alarms = Ctx.GetSystemService(Context.AlarmService) as AlarmManager;
        var next = alarms?.NextAlarmClock;
        return Task.FromResult(next is null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeMilliseconds(next.TriggerTime));
    }

    public Task<string> AddEventAsync(DeviceEvent e)
    {
        var intent = new Intent(Intent.ActionInsert);
        intent.SetData(CalendarContract.Events.ContentUri);
        intent.PutExtra("beginTime", e.Start.ToUnixTimeMilliseconds());
        intent.PutExtra("endTime", e.End.ToUnixTimeMilliseconds());
        intent.PutExtra("allDay", e.AllDay);
        intent.PutExtra("title", e.Title);
        if (e.Location is { } location) intent.PutExtra("eventLocation", location);
        if (e.Notes is { } notes) intent.PutExtra("description", notes);
        Start(intent, "Nenhum app de agenda neste aparelho aceita eventos.");
        return Task.FromResult("O app de agenda abriu com o evento “" + e.Title + "”; ele fica salvo quando a pessoa confirmar lá.");
    }

    public async Task<IReadOnlyList<DeviceEvent>> EventsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        if (Ctx.CheckSelfPermission(Android.Manifest.Permission.ReadCalendar) != Permission.Granted && !await CalendarPermissionActivity.RequestAsync())
            throw new AiToolException("A pessoa não permitiu que o Numa lesse a agenda. Ela pode permitir nas configurações do Android.");
        return await Task.Run(() =>
        {
            var builder = CalendarContract.Instances.ContentUri!.BuildUpon()!;
            ContentUris.AppendId(builder, from.ToUnixTimeMilliseconds());
            ContentUris.AppendId(builder, to.ToUnixTimeMilliseconds());
            var list = new List<DeviceEvent>();
            using var cursor = Ctx.ContentResolver?.Query(builder.Build()!, ["title", "begin", "end", "eventLocation", "allDay"], null, null, "begin ASC");
            while (cursor is not null && cursor.MoveToNext())
            {
                var title = cursor.GetString(0) ?? "(sem título)";
                var begin = DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(1));
                var end = DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(2));
                var location = cursor.GetString(3);
                list.Add(new DeviceEvent(title, begin, end, string.IsNullOrWhiteSpace(location) ? null : location, null, cursor.GetInt(4) != 0));
            }
            return (IReadOnlyList<DeviceEvent>)list;
        });
    }

    public Task<string> ComposeEmailAsync(IReadOnlyList<string> to, string subject, string body)
    {
        var intent = new Intent(Intent.ActionSendto, Android.Net.Uri.Parse("mailto:"));
        intent.PutExtra(Intent.ExtraEmail, to.ToArray());
        intent.PutExtra(Intent.ExtraSubject, subject);
        intent.PutExtra(Intent.ExtraText, body);
        Start(intent, "Nenhum app de e-mail neste aparelho.");
        return Task.FromResult("A mensagem está pronta no app de e-mail; a pessoa revisa e envia lá. Nada foi enviado pelo Numa.");
    }
}

/// <summary>NN-016: the calendar is read only after the system asks the person, once, over the app.</summary>
[Activity(Theme = "@style/UrbeTransparent", Exported = false, ExcludeFromRecents = true, NoHistory = true)]
public sealed class CalendarPermissionActivity : Activity
{
    private const int Request = 8467;
    private static TaskCompletionSource<bool>? _pending;

    internal static Task<bool> RequestAsync()
    {
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending?.TrySetResult(false);
        _pending = pending;
        var intent = new Intent(Application.Context, typeof(CalendarPermissionActivity));
        intent.AddFlags(ActivityFlags.NewTask);
        Application.Context.StartActivity(intent);
        return pending.Task;
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestPermissions([Android.Manifest.Permission.ReadCalendar], Request);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != Request) return;
        _pending?.TrySetResult(grantResults.Length > 0 && grantResults[0] == Permission.Granted);
        _pending = null;
        Finish();
    }

    protected override void OnDestroy()
    {
        // closed without an answer (back pressed): never left waiting
        _pending?.TrySetResult(false);
        _pending = null;
        base.OnDestroy();
    }
}
