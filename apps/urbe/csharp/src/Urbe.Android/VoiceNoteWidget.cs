using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace Urbe.AndroidHost;

/// <summary>
/// UC-56/UC-66: home-screen widget. One tap starts recording a quick note in <see cref="VoiceNoteService"/> without
/// opening the app; a second tap stops it. Before the microphone is allowed, the tap opens the tiny
/// <see cref="VoicePermissionActivity"/> instead, which asks once and then starts recording.
/// </summary>
[BroadcastReceiver(Label = "Urbe: nota rápida", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/urbe_voice_widget")]
public sealed class VoiceNoteWidget : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is null) return;
        var views = Views(context, VoiceNoteService.IsRecording);
        foreach (var id in appWidgetIds) appWidgetManager.UpdateAppWidget(id, views);
    }

    /// <summary>Redraws every placed widget (recording or idle; tap target follows the permission).</summary>
    public static void Refresh(Context context, bool recording)
    {
        try
        {
            var manager = AppWidgetManager.GetInstance(context);
            var ids = manager?.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(VoiceNoteWidget))));
            if (manager is null || ids is null || ids.Length == 0) return;
            var views = Views(context, recording);
            foreach (var id in ids) manager.UpdateAppWidget(id, views);
        }
        catch (Java.Lang.Exception) { }
    }

    private static RemoteViews Views(Context context, bool recording)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.urbe_voice_widget);
        views.SetTextViewText(Resource.Id.urbe_voice_widget_icon, recording ? "🔴" : "🎤");
        views.SetTextViewText(Resource.Id.urbe_voice_widget_label, recording ? "Gravando…" : "Nota rápida");
        views.SetInt(Resource.Id.urbe_voice_widget_root, "setBackgroundResource",
            recording ? Resource.Drawable.urbe_voice_widget_bg_rec : Resource.Drawable.urbe_voice_widget_bg);
        views.SetOnClickPendingIntent(Resource.Id.urbe_voice_widget_root, TapIntent(context));
        return views;
    }

    /// <summary>The tap: the recording service when the microphone is allowed, else the permission screen.</summary>
    private static PendingIntent? TapIntent(Context context)
    {
        const PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
        if (!VoiceNotes.HasMicrophone(context))
        {
            var ask = new Intent(context, typeof(VoicePermissionActivity));
            ask.SetFlags(ActivityFlags.NewTask | ActivityFlags.NoAnimation | ActivityFlags.ExcludeFromRecents);
            return PendingIntent.GetActivity(context, 1, ask, flags);
        }
        var toggle = VoiceNoteService.ToggleIntent(context);
        return OperatingSystem.IsAndroidVersionAtLeast(26)
            ? PendingIntent.GetForegroundService(context, 2, toggle, flags)
            : PendingIntent.GetService(context, 2, toggle, flags);
    }
}
