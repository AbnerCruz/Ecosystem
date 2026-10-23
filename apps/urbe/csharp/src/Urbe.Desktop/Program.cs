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
        UrbeApp.OpeningNamesPath = Path.Combine(Path.GetDirectoryName(selection)!, "opening-names.json");
        UrbeApp.AssistantFolder = Path.Combine(Path.GetDirectoryName(selection)!, "assistant");
        // Imprimir ou salvar PDF: the page in the browser, which opens its print dialog (the 1.8.4's print frame)
        ContentViewers.Printer = (title, html) =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-print");
            Directory.CreateDirectory(dir);
            var name = string.Concat((string.IsNullOrWhiteSpace(title) ? "pagina" : title).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
            var file = Path.Combine(dir, name + ".html");
            File.WriteAllText(file, html.Replace("</body>", "<script>addEventListener(\"load\",function(){(document.fonts&&document.fonts.ready||Promise.resolve()).then(function(){setTimeout(function(){print()},250)})})</script></body>"));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true });
            return Task.CompletedTask;
        };
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
