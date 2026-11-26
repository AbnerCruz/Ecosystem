using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Gerar APK in the shell (P5): the game's Android identity (name, package, orientation) on one page, then one button
/// that compiles, packs Content/ and Assets/ and signs on the device (no login, no internet), and a save dialog for
/// the .apk. Compile errors are listed with their lines. The packing and the signing key belong to the Android head
/// (<see cref="LunetShell.PackageGameApk"/>); without it the page says that an APK is made on the phone.
/// </summary>
public sealed class ApkView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Action _close;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private CancellationTokenSource? _cancel;
    private string? _status;
    private string _statusColor = LunetTokens.Color.TextSecondary;
    private IReadOnlyList<Lunet.Compiler.LunetDiagnostic> _errors = [];
    private byte[]? _apk;

    public ApkView(LunetShell shell, Action close)
    {
        _shell = shell;
        _close = close;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Close());
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        var title = LunetUi.Title("Gerar APK");
        title.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(title);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = new Border { Child = _page, MaxWidth = 760, Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.Xl) } });
        Content = root;
        Refresh();
    }

    private LunetProject Project => _shell.Workspace.Project!;
    public bool Busy => _cancel is not null;
    public string? Status => _status;
    public IReadOnlyList<Lunet.Compiler.LunetDiagnostic> Errors => _errors;

    public bool Close() { _cancel?.Cancel(); _close(); return true; }

    /// <summary>Another game already uses this Android identity (one would replace the other when installed).</summary>
    public string? IdentityConflict => _shell.Workspace.Store.FindIdentityConflict(Project.Manifest, Project.Directory);

    public void SetPackage(string package)
    {
        package = package.Trim();
        if (!OfflineGameApkBuilder.ValidPackage(package)) throw new ArgumentException("Use o formato com.exemplo.meujogo (letras minúsculas, números e pontos).");
        SaveManifest(m => m.PackageId = package);
    }

    public void SetOrientation(string orientation) => SaveManifest(m => m.Orientation = orientation == "landscape" ? "landscape" : "portrait");

    public void SeparateIdentity() { _shell.Workspace.Store.ForkIdentity(Project); Say("Este jogo agora tem uma identidade própria.", LunetTokens.Color.Success); }

    private void SaveManifest(Action<ProjectManifest> change)
    {
        var json = Project.Manifest.ToJson();
        change(Project.Manifest);
        try { Project.WriteText(ProjectStore.ManifestFileName, Project.Manifest.ToJson()); }
        catch
        {
            // Nothing half-changed: the manifest in memory goes back to what is on disk.
            var previous = ProjectManifest.Parse(json);
            Project.Manifest.PackageId = previous.PackageId;
            Project.Manifest.Orientation = previous.Orientation;
            throw;
        }
        Say("Salvo.", LunetTokens.Color.Success);
    }

    /// <summary>Compiles, gathers Content/ and Assets/ and packs a signed APK. Null with <see cref="Status"/> when it cannot.</summary>
    public async Task<byte[]?> BuildAsync()
    {
        if (Busy) return null;
        if (_shell.PackageGameApk is not { } pack) { Say("O APK é gerado no aparelho Android: abra este jogo no Lunet do celular.", LunetTokens.Color.Warning); return null; }
        if (!OfflineGameApkBuilder.ValidPackage(Project.Manifest.PackageId)) { Say("Corrija o identificador Android antes.", LunetTokens.Color.Danger); return null; }
        if (IdentityConflict is { } other) { Say($"\"{other}\" usa a mesma identidade. Separe as identidades antes de gerar.", LunetTokens.Color.Danger); return null; }
        _cancel = new CancellationTokenSource();
        var ct = _cancel.Token;
        _apk = null;
        _errors = [];
        try
        {
            _shell.SaveAll();
            Say("Compilando o código…", LunetTokens.Color.TextSecondary);
            var project = Project;
            var compiled = await Task.Run(() => _shell.Workspace.Compile(), ct);
            if (!compiled.Success || compiled.Assembly is null)
            {
                _errors = compiled.Diagnostics.Where(d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Error).ToArray();
                Say($"O jogo não compila: {_errors.Count} erro(s). Corrija e tente de novo.", LunetTokens.Color.Danger);
                return null;
            }
            Say("Empacotando o jogo e os recursos e assinando…", LunetTokens.Color.TextSecondary);
            var apk = await Task.Run(() =>
            {
                var files = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["game/Game.dll"] = compiled.Assembly };
                foreach (var relative in project.ListFiles())
                {
                    if (!relative.StartsWith("Content/", StringComparison.Ordinal) && !relative.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                    ct.ThrowIfCancellationRequested();
                    files.Add("game/" + relative, File.ReadAllBytes(Path.Combine(project.Directory, relative.Replace('/', Path.DirectorySeparatorChar))));
                }
                ct.ThrowIfCancellationRequested();
                return pack(project, files);
            }, ct);
            _apk = apk;
            Say($"APK pronto ({apk.Length / 1024} KB). Salve o arquivo e abra-o no aparelho para instalar.", LunetTokens.Color.Success);
            return apk;
        }
        catch (OperationCanceledException) { Say("Geração cancelada.", LunetTokens.Color.TextSecondary); return null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or ProjectException)
        {
            Say("Não foi possível gerar: " + e.Message, LunetTokens.Color.Danger);
            return null;
        }
        finally { _cancel?.Dispose(); _cancel = null; Refresh(); }
    }

    public void Cancel() => _cancel?.Cancel();

    private async Task SaveApk()
    {
        if (_apk is not { } apk) return;
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage) { Say("Este aparelho não oferece onde salvar arquivos.", LunetTokens.Color.Danger); return; }
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Salvar APK", SuggestedFileName = Project.Name + ".apk", DefaultExtension = "apk",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("APK Android") { Patterns = ["*.apk"], MimeTypes = ["application/vnd.android.package-archive"] }],
        });
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(apk);
            Say("APK salvo. Abra o arquivo para instalar o jogo.", LunetTokens.Color.Success);
            Haptics.Confirm?.Invoke();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Say("Não foi possível salvar: " + e.Message, LunetTokens.Color.Danger); }
    }

    private void Say(string text, string color) { _status = text; _statusColor = color; Refresh(); }

    private void Refresh()
    {
        _page.Children.Clear();
        var manifest = Project.Manifest;
        _page.Children.Add(LunetUi.Secondary("Gera um app Android só deste jogo, no próprio aparelho: sem conta, sem internet. Uma nova versão instala por cima da anterior."));

        if (_status is { } status) _page.Children.Add(Notice(status, _statusColor));
        foreach (var d in _errors.Take(8))
            _page.Children.Add(Notice($"{d.FilePath} · linha {d.Line}: {d.Message}", LunetTokens.Color.Danger));

        _page.Children.Add(Section("Jogo"));
        _page.Children.Add(LunetUi.Text(Project.Name, LunetTokens.Type.Title, weight: FontWeight.SemiBold));
        _page.Children.Add(LunetUi.Text("Identificador Android", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var package = new TextBox { Text = manifest.PackageId, MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.MonoFont, VerticalContentAlignment = VerticalAlignment.Center, Name = "PackageId" };
        AutomationProperties.SetName(package, "Identificador Android");
        package.LostFocus += (_, _) => { if ((package.Text ?? "").Trim() != Project.Manifest.PackageId) Run(() => SetPackage(package.Text ?? "")); };
        _page.Children.Add(package);

        _page.Children.Add(LunetUi.Text("Orientação", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var orientation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
        foreach (var (value, label) in new[] { ("portrait", "Vertical"), ("landscape", "Horizontal") })
        {
            var v = value;
            var b = LunetUi.Button(label, manifest.Orientation == value ? ButtonKind.Selected : ButtonKind.Secondary, null, () => Run(() => SetOrientation(v)));
            b.Name = "Orientation-" + value;
            orientation.Children.Add(b);
        }
        _page.Children.Add(orientation);

        if (IdentityConflict is { } conflict)
        {
            _page.Children.Add(Notice($"\"{conflict}\" usa a mesma identidade Android: instalado, um jogo substituiria o outro.", LunetTokens.Color.Warning));
            var separate = LunetUi.Button("Separar a identidade deste jogo", ButtonKind.Secondary, null, () => Run(SeparateIdentity));
            separate.Name = "SeparateIdentity";
            separate.HorizontalAlignment = HorizontalAlignment.Left;
            _page.Children.Add(separate);
        }

        var build = Busy
            ? LunetUi.Button("Cancelar", ButtonKind.Secondary, LunetIcons.Close, Cancel)
            : LunetUi.Button("Gerar APK", ButtonKind.Primary, LunetIcons.Download, () => _ = BuildAsync());
        build.Name = "BuildApk";
        build.MinHeight = 56;
        build.HorizontalAlignment = HorizontalAlignment.Stretch;
        build.Margin = new Thickness(0, LunetTokens.Space.M, 0, 0);
        _page.Children.Add(build);
        if (_apk is not null && !Busy)
        {
            var save = LunetUi.Button("Salvar o APK", ButtonKind.Secondary, LunetIcons.Share, () => _ = SaveApk());
            save.Name = "SaveApk";
            save.MinHeight = 52;
            save.HorizontalAlignment = HorizontalAlignment.Stretch;
            _page.Children.Add(save);
        }
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or ProjectException or InvalidDataException)
        {
            Say(e.Message, LunetTokens.Color.Danger);
        }
    }

    private static TextBlock Section(string text) => LunetUi.Section(text);

    private static Control Notice(string text, string color) => LunetUi.Notice(text, color);
}
