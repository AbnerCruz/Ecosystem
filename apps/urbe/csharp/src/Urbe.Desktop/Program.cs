using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Urbe.Client;

namespace Urbe.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // The vault is a folder of the computer, chosen with the system folder picker and
        // remembered in the app data (never a hidden copy of the notes).
        var selection = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Urbe", "native-vault.json");
        UrbeApp.VaultStorageFactory = () => new FolderVaultStorage(PickFolderAsync, selection);
        UrbeApp.LocalStatePath = Path.Combine(Path.GetDirectoryName(selection)!, "notes-state.json");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<UrbeApp>().UsePlatformDetect();

    private static async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            return null;
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Escolha a pasta do vault",
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
