using Android.App;
using Android.Content;
using AndroidUri = Android.Net.Uri;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Avalonia.Android;
using Urbe.Client;

namespace Urbe.AndroidHost;

[Application(Label = "Urbe nativo (prévia)", Icon = "@drawable/icon")]
public sealed class UrbeAndroidApplication : AvaloniaAndroidApplication<UrbeApp>
{
    public UrbeAndroidApplication(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
        UrbeApp.VaultStorageFactory = () => new AndroidVaultStorage();
    }
}

[Activity(
    Label = "Urbe nativo (prévia)",
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
    private TaskCompletionSource<AndroidUri?>? _folderPicker;
    public static MainActivity? Current { get; private set; }

    protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
    {
        Current = this;
        base.OnCreate(savedInstanceState);
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Current, this)) Current = null;
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
