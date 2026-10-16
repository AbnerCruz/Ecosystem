using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace Urbe.AndroidHost;

/// <summary>
/// UC-56: home-screen widget with a 🎤 that opens the Urbe and starts a quick voice note
/// (MainActivity reads <see cref="MainActivity.VoiceExtra"/>). No state of its own.
/// </summary>
[BroadcastReceiver(Label = "Urbe: nota rápida", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/urbe_voice_widget")]
public sealed class VoiceNoteWidget : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is null) return;
        var intent = new Intent(context, typeof(MainActivity));
        intent.PutExtra(MainActivity.VoiceExtra, true);
        // reuse the running activity (OnNewIntent) instead of stacking another
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        var pending = PendingIntent.GetActivity(context, 0, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var views = new RemoteViews(context.PackageName, Resource.Layout.urbe_voice_widget);
        views.SetOnClickPendingIntent(Resource.Id.urbe_voice_widget_root, pending);
        foreach (var id in appWidgetIds) appWidgetManager.UpdateAppWidget(id, views);
    }
}
