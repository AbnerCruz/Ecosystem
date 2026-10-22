using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;

namespace Urbe.AndroidHost;

/// <summary>
/// UC-66 (NN-016): the first widget tap without the microphone allowed. Transparent over the home screen: one
/// friendly line says why, the system asks once, and a yes starts the recording right away. Nothing else opens.
/// </summary>
[Activity(Theme = "@style/UrbeTransparent", Exported = false, ExcludeFromRecents = true, NoHistory = true,
    TaskAffinity = "app.urbe.voice", LaunchMode = LaunchMode.SingleTask)]
public sealed class VoicePermissionActivity : Activity
{
    private const int Request = 8466;
    private const string AskedKey = "urbe.voice.asked";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (VoiceNotes.HasMicrophone(this)) { StartRecording(); return; }
        var prefs = GetSharedPreferences("urbe-voice", FileCreationMode.Private);
        bool asked = prefs?.GetBoolean(AskedKey, false) ?? false;
        // denied for good ("não perguntar de novo"): the system will not show its dialog, so point to the settings
        if (asked && !ShouldShowRequestPermissionRationale(Android.Manifest.Permission.RecordAudio))
        {
            Toast.MakeText(this, "Permita o microfone do Urbe nas configurações para gravar notas rápidas.", ToastLength.Long)?.Show();
            try
            {
                var settings = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings, Android.Net.Uri.Parse("package:" + PackageName));
                settings.AddFlags(ActivityFlags.NewTask);
                StartActivity(settings);
            }
            catch (ActivityNotFoundException) { }
            Finish();
            return;
        }
        prefs?.Edit()?.PutBoolean(AskedKey, true)?.Apply();
        Toast.MakeText(this, "Para gravar sua nota rápida, o Urbe usa o microfone só enquanto você fala.", ToastLength.Long)?.Show();
        RequestPermissions(VoiceNotes.Permissions(), Request);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != Request) return;
        VoiceNoteWidget.Refresh(this, recording: false);
        if (VoiceNotes.HasMicrophone(this)) StartRecording();
        else
        {
            Toast.MakeText(this, "Sem o microfone, o widget não consegue gravar.", ToastLength.Short)?.Show();
            Finish();
        }
    }

    /// <summary>Started while this screen is visible, so the microphone service is allowed to start.</summary>
    private void StartRecording()
    {
        var intent = VoiceNoteService.StartIntent(this);
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) StartForegroundService(intent);
        else StartService(intent);
        Finish();
    }
}
