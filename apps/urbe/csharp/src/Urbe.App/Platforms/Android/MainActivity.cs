using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;

namespace Urbe.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int OpenVaultTreeRequest = 8416;
    private TaskCompletionSource<Uri?>? _folderPicker;

    public Task<Uri?> PickVaultFolderAsync()
    {
        if (_folderPicker is not null)
            throw new InvalidOperationException("Já existe uma seleção de pasta em andamento.");

        _folderPicker = new TaskCompletionSource<Uri?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

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
        if (requestCode != OpenVaultTreeRequest)
            return;

        var pending = _folderPicker;
        _folderPicker = null;
        if (pending is null)
            return;

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
                throw new UnauthorizedAccessException(
                    "A pasta precisa autorizar leitura e gravação.");

            ContentResolver!.TakePersistableUriPermission(data.Data, allowed);
            pending.TrySetResult(data.Data);
        }
        catch (Exception exception)
        {
            pending.TrySetException(exception);
        }
    }
}
