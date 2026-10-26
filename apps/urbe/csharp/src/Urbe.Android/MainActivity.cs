using Android.App;
using Android.Content;
using AndroidUri = Android.Net.Uri;
using Android.Content.PM;
using Android.Runtime;
using Android.Speech;
using Android.Views;
using Avalonia.Android;
using Urbe.Client;

// the Assistente talks to the model provider the person connects (OpenRouter, Anthropic, OpenAI, Gemini, Ollama…)
[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
// UC-56 (NN-016): the in-app "Nota rápida" asks the system recognizer (RecognizerIntent) by intent; that app holds the
// microphone and only the text comes back. The home-screen widget records by itself (UC-66): its permissions
// (RECORD_AUDIO, foreground service of type microphone, notifications) are declared in VoiceNoteService.cs.

namespace Urbe.AndroidHost;

/// <summary>The name on the launcher: the beta channel (DEC-0048) or the preview built for each PR.</summary>
internal static class AppInfo
{
#if URBE_BETA
    public const string Name = "Urbe";
#else
    public const string Name = "Urbe nativo (prévia)";
#endif
}

[Application(Label = AppInfo.Name, Icon = "@drawable/icon")]
public sealed class UrbeAndroidApplication : AvaloniaAndroidApplication<UrbeApp>
{
    public UrbeAndroidApplication(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
        UrbeApp.VaultStorageFactory = () => new AndroidVaultStorage();
        // HTML, PDF, audio and video seen inside the Urbe (ADD-0023)
        AndroidContentViewers.Register();
        // the Notas state kept on the device (favourites, recents), in the app's private files
        UrbeApp.LocalStatePath = System.IO.Path.Combine(Android.App.Application.Context.FilesDir?.AbsolutePath ?? System.IO.Path.GetTempPath(), "notes-state.json");
        UrbeApp.OpeningNamesPath = System.IO.Path.Combine(Android.App.Application.Context.FilesDir?.AbsolutePath ?? System.IO.Path.GetTempPath(), "opening-names.json");
        // the Assistente's conversations, providers and keys: only in the app's private files, never in the vault
        // the errors nobody caught: written to the app's files and kept from closing the app (Configurações → Relatório de erros)
        CrashLog.FilePath = System.IO.Path.Combine(Android.App.Application.Context.FilesDir?.AbsolutePath ?? System.IO.Path.GetTempPath(), "erros.log");
        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => { CrashLog.Write(e.Exception, "android"); e.Handled = true; };
        UrbeApp.AssistantFolder = System.IO.Path.Combine(Android.App.Application.Context.FilesDir?.AbsolutePath ?? System.IO.Path.GetTempPath(), "assistant");
        // the AI that answers without the internet: the model in the app's private files, the engine in its native library folder
        try
        {
            var context = Android.App.Application.Context;
            if (context.ApplicationInfo?.NativeLibraryDir is { } nativeDir) Urbe.LocalAi.LlamaTextRuntime.UseNativeFolder(nativeDir);
            Urbe.Core.Assistant.LocalAi.Text = new Urbe.LocalAi.LlamaTextRuntime(
                System.IO.Path.Combine(context.FilesDir?.AbsolutePath ?? System.IO.Path.GetTempPath(), "local-ai"), GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
        }
        catch (Exception) { /* without the engine the Assistente keeps to the providers on the internet */ }
        // Configurações → Verificar atualização: the APK of the newest direct release, over the installed one
        UrbeApp.UpdateAsset = Urbe.Core.AppUpdate.AndroidAsset;
        try
        {
            var context = Android.App.Application.Context;
#pragma warning disable CA1422, CS0618 // the flags overload is the one every supported Android has
            UrbeApp.InstalledVersion = context.PackageManager?.GetPackageInfo(context.PackageName ?? "", (Android.Content.PM.PackageInfoFlags)0)?.VersionName;
#pragma warning restore CA1422, CS0618
        }
        catch (Exception) { /* the check still shows the newest release */ }
    }
}

[Activity(
    Label = AppInfo.Name,
    Theme = "@style/UrbeTheme",
    Icon = "@drawable/icon",
    MainLauncher = true,
    // Soft keyboard resizes the view so the editor caret stays visible (1.8.4 REQ-107 behaviour).
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public sealed class MainActivity : AvaloniaMainActivity
{
    private const int OpenVaultTreeRequest = 8416;
    private const int SpeechRequest = 8417;
    private const int AiNotificationPermissionRequest = 8418;
    private bool _openAssistantRequested;
    public static bool IsVisibleToUser { get; private set; }
    /// <summary>The intent extra the home-screen widget sets to start a quick voice note (UC-56).</summary>
    public const string VoiceExtra = "urbe.voice";
    private TaskCompletionSource<AndroidUri?>? _folderPicker;
    private TaskCompletionSource<string?>? _speech;
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
    {
        Current = this;
        base.OnCreate(savedInstanceState);
        OnBackPressedDispatcher.AddCallback(this, new UrbeBackCallback(this));
        PlatformAssistantActivity.Publish = AssistantRunService.Publish;
        PlatformAssistantActivity.RequestNotificationPermission = RequestAiNotificationPermission;
        // the editor asks for the keyboard when a block becomes an edit box: Android does not always open it by itself (UC-48)
        PlatformInput.ShowKeyboard = () => Window?.DecorView is { } root && GetSystemService(InputMethodService) is Android.Views.InputMethods.InputMethodManager ime
            ? ShowKeyboard(root, ime) : false;
        // UC-56: the system recognizer listens and records (its own app and permission); this app gets only the text
        PlatformVoice.Transcribe = () => Current is { } activity ? activity.RecognizeSpeechAsync() : Task.FromResult<string?>(null);
        // UC-66: the widget's voice notes not yet in the vault are offered again ("Tentar") once the vault is shown
        PlatformVoice.Backlog = VoiceNotes.Backlog(ApplicationContext ?? this);
        VoiceNoteWidget.Refresh(this, VoiceNoteService.IsRecording);
        // UC-69 (UX.md P7): haptics through the view, which follows the system touch-feedback setting and needs no VIBRATE permission
        Urbe.Client.Ui.PlatformHaptics.Perform = kind => Current?.Window?.DecorView?.PerformHapticFeedback(Haptic(kind));
        // "Remover animações" (animator duration scale 0) turns the interface's motion off (P6, P12)
        UrbeTokens.Motion.Reduced = Android.Provider.Settings.Global.GetFloat(ContentResolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
        HandleVoiceIntent(Intent);
        HandleAssistantIntent(Intent);
    }

    /// <summary>AndroidX dispatcher handles gesture/physical Back on modern Android without the obsolete override.</summary>
    private sealed class UrbeBackCallback(MainActivity activity) : AndroidX.Activity.OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            if (UrbeApp.ActiveView?.HandleSystemBack() == true) return;
            // At the root, delegate to the system/default dispatcher without calling ourselves again.
            Enabled = false;
            try { activity.OnBackPressedDispatcher.OnBackPressed(); }
            finally { Enabled = true; }
        }
    }

    private static FeedbackConstants Haptic(Urbe.Client.Ui.HapticKind kind) => kind switch
    {
        Urbe.Client.Ui.HapticKind.Hold => FeedbackConstants.LongPress,
        Urbe.Client.Ui.HapticKind.Confirm => OperatingSystem.IsAndroidVersionAtLeast(30) ? FeedbackConstants.Confirm : FeedbackConstants.ContextClick,
        Urbe.Client.Ui.HapticKind.Error => OperatingSystem.IsAndroidVersionAtLeast(30) ? FeedbackConstants.Reject : FeedbackConstants.LongPress,
        _ => FeedbackConstants.KeyboardTap
    };

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleVoiceIntent(intent);
        HandleAssistantIntent(intent);
    }

    private void HandleAssistantIntent(Intent? intent)
    {
        if (intent?.Action != "app.urbe.ai.OPEN") return;
        _openAssistantRequested = true;
        intent.SetAction(null);
        if (IsVisibleToUser) ShowRequestedAssistant();
    }

    private void ShowRequestedAssistant()
    {
        if (!_openAssistantRequested) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (UrbeApp.ActiveView is not { } view) return;
            _openAssistantRequested = false;
            view.Assistant.Open();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void RequestAiNotificationPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) ||
            CheckSelfPermission("android.permission.POST_NOTIFICATIONS") == Permission.Granted) return;
        RequestPermissions(["android.permission.POST_NOTIFICATIONS"], AiNotificationPermissionRequest);
    }

    /// <summary>
    /// An intent with <see cref="VoiceExtra"/> (the UC-56 widget's old tap; the widget now records by itself, UC-66):
    /// the quick note starts once the vault is shown; the extra is consumed so it runs once.
    /// </summary>
    private static void HandleVoiceIntent(Intent? intent)
    {
        if (intent is null || !intent.GetBooleanExtra(VoiceExtra, false)) return;
        intent.RemoveExtra(VoiceExtra);
        PlatformVoice.RequestQuickNote();
    }

    /// <summary>RecognizerIntent.ACTION_RECOGNIZE_SPEECH in pt-BR: the best result, or null when cancelled.</summary>
    public Task<string?> RecognizeSpeechAsync()
    {
        if (_speech is not null) return Task.FromResult<string?>(null);
        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, "pt-BR");
        intent.PutExtra(RecognizerIntent.ExtraPrompt, "Fale sua nota rápida");
        intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _speech = pending;
        try { StartActivityForResult(intent, SpeechRequest); }
        catch (ActivityNotFoundException)
        {
            _speech = null;
            throw new InvalidOperationException("Este aparelho não tem um reconhecedor de voz. Instale ou ative o app de digitação por voz do Google (ou outro) e tente de novo.");
        }
        return pending.Task;
    }

    private static bool ShowKeyboard(View root, Android.Views.InputMethods.InputMethodManager ime)
    {
        var target = root.FindFocus() ?? root;
        return ime.ShowSoftInput(target, Android.Views.InputMethods.ShowFlags.Implicit);
    }

    /// <summary>workspace.js flushPending on visibilitychange hidden: write a pending map now.</summary>
    protected override void OnResume()
    {
        base.OnResume();
        IsVisibleToUser = true;
        ShowRequestedAssistant();
    }

    protected override void OnPause()
    {
        IsVisibleToUser = false;
        UrbeApp.FlushPendingOnBackground();
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
            IsVisibleToUser = false;
        }
        _speech?.TrySetResult(null);
        _speech = null;
        base.OnDestroy();
    }

    public Task<AndroidUri?> PickVaultFolderAsync()
    {
        if (_folderPicker is not null)
            throw new InvalidOperationException("Já existe uma seleção de pasta em andamento.");
        _folderPicker = new TaskCompletionSource<AndroidUri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission |
                            ActivityFlags.GrantWriteUriPermission |
                            ActivityFlags.GrantPersistableUriPermission |
                            ActivityFlags.GrantPrefixUriPermission);
            StartActivityForResult(intent, OpenVaultTreeRequest);
        }
        catch (Exception exception)
        {
            _folderPicker.TrySetException(exception);
            _folderPicker = null;
            throw;
        }
        return _folderPicker.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == SpeechRequest)
        {
            var speech = _speech;
            _speech = null;
            speech?.TrySetResult(resultCode == Result.Ok
                ? data?.GetStringArrayListExtra(RecognizerIntent.ExtraResults)?.FirstOrDefault()
                : null);
            return;
        }
        if (requestCode != OpenVaultTreeRequest) return;
        var pending = _folderPicker;
        _folderPicker = null;
        if (pending is null) return;
        if (resultCode != Result.Ok || data?.Data is null)
        {
            pending.TrySetResult(null);
            return;
        }
        try
        {
            var allowed = data.Flags &
                (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            if (!allowed.HasFlag(ActivityFlags.GrantReadUriPermission) ||
                !allowed.HasFlag(ActivityFlags.GrantWriteUriPermission))
                throw new UnauthorizedAccessException("A pasta precisa autorizar leitura e gravação.");
            ContentResolver!.TakePersistableUriPermission(data.Data, allowed);
            pending.TrySetResult(data.Data);
        }
        catch (Exception exception) { pending.TrySetException(exception); }
    }
}
