using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>Nova pasta, Importar, the backups and the vault's maintenance, driven through the real native UI against a real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    private static async Task<(MainView View, Window Window, string Vault, string Dir)> OpenCreateVault()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-create-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
        File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
        var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
        var view = new MainView(storage);
        var window = new Window { Width = 1280, Height = 760, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        await Settle(window, view);
        return (view, window, vault, dir);
    }

    /// <summary>An item of the Explorer's "Mais opções" (moreMenu), with <paramref name="next"/> answering what follows.</summary>
    private static Task PickFromNotesMenu(MainView view, string item, Func<Task> next) => PickFromMenu(view, view.ShowNotesMenuAsync, item, next);

    /// <summary>An item of the Explorer's "Nova" (newMenu: "Criar").</summary>
    private static Task PickFromNewMenu(MainView view, string item, Func<Task> next) => PickFromMenu(view, view.ShowNotesCreateMenuAsync, item, next);

    private static async Task PickFromMenu(MainView view, Func<Task> open, string item, Func<Task> next)
    {
        var menu = open();
        Dispatcher.UIThread.RunJobs();
        ClickButtonWithText(view.Dialogs, item);
        await Task.Delay(30);
        Dispatcher.UIThread.RunJobs();
        await next();
        // a restore reopens the vault: frames are ticked until the flow ends
        for (int i = 0; i < 600 && !menu.IsCompleted; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        if (!menu.IsCompleted)
            throw new TimeoutException("A ação não terminou. Na tela: " + string.Join(" | ", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text)));
        await menu;
    }

    /// <summary>A flow started directly (what the Explorer's Nova pasta does), with <paramref name="next"/> answering its dialog.</summary>
    private static async Task RunWithDialog(Task flow, Func<Task> next)
    {
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(30);
        Dispatcher.UIThread.RunJobs();
        await next();
        for (int i = 0; i < 1500 && !flow.IsCompleted; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        await flow;
    }

    [Fact]
    public async Task NewFolder_IsAFolderInTheVaultAndInTheExplorer() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().ToArray();
                await RunWithDialog(view.CreateFolderAsync(), () =>
                {
                    Snapshot(window, "native-new-folder-dialog.png");
                    var input = view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single();
                    input.Text = "  Projetos ";
                    ClickButtonWithText(view.Dialogs, "Criar pasta");
                    return Task.CompletedTask;
                });
                Assert.True(Directory.Exists(Path.Combine(vault, "Projetos")));
                Assert.Contains("Projetos", view.Library!.Folders);
                Assert.Contains("Projetos", view.Explorer.VisibleFolderPaths);
                await UntilNotesSaved(view);
                // no note touched; only the notes' record (and the identity derived from it) is added
                Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + ".urbe" + Path.DirectorySeparatorChar)).Order().ToArray());

                // the same name again: "projetos (2)" (v21UniqueRegionName), never the same folder
                await RunWithDialog(view.CreateFolderAsync(), () =>
                {
                    view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "projetos";
                    ClickButtonWithText(view.Dialogs, "Criar pasta");
                    return Task.CompletedTask;
                });
                Assert.True(Directory.Exists(Path.Combine(vault, "projetos (2)")));

                // an empty folder is still there after the vault is opened again (.urbe/notas.json)
                await UntilNotesSaved(view);
                await view.OpenVaultAsync();
                await Settle(window, view);
                Assert.Contains("Projetos", view.Library!.Folders);
                Assert.Equal(("Projetos/Ideias.md", "# "), view.Library.PlanNewNote("Projetos", "Ideias"));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task NewFolderAndImport_WithoutAWritableVaultCreateNothingInMemory() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            int folders = view.Library!.Folders.Count, notes = view.Documents!.List().Count;
            foreach (var flow in new Func<Task>[] { () => view.CreateFolderAsync(), () => view.ImportAsync() })
                await RunWithDialog(flow(), () =>
                {
                    Assert.Empty(view.Dialogs.GetVisualDescendants().OfType<TextBox>());
                    ClickButtonWithText(view.Dialogs, "Ok");
                    return Task.CompletedTask;
                });
            Assert.Equal(folders, view.Library.Folders.Count);
            Assert.Equal(notes, view.Documents.List().Count);
            window.Close();
            return true;
        }, CancellationToken.None));

    private static ImportSelection Files(bool directory, params (string Path, string Text)[] files) =>
        new(files.Select(f => new ImportSource(f.Path, _ => ValueTask.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(f.Text))))).ToList(), directory);

    [Fact]
    public async Task Import_WritesNewDocumentsIntoTheVaultAndKeepsWhatIsThereOnConflict() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                int notes = view.Documents!.List().Count;
                view.ImportPicker = folder => Task.FromResult<ImportSelection?>(Files(folder,
                    ("Novo.md", "# Novo\n"), ("Pacote/Dentro.md", "# Dentro\n"), ("Comece.md", "# OUTRO\n"), ("Bairro/Um.md", "# Um\n"),
                    ("foto.bin", "x"), (".obsidian/app.json", "{}")));
                await PickFromNotesMenu(view, "Importar arquivos", async () =>
                {
                    ClickButtonWithText(view.Dialogs, "Pasta inteira");
                    await Task.Delay(50);
                    Dispatcher.UIThread.RunJobs();
                    Snapshot(window, "native-import-confirm.png");
                    ClickButtonWithText(view.Dialogs, "Continuar");
                    await Task.Delay(300);
                    Dispatcher.UIThread.RunJobs();
                });
                Assert.Equal("# Novo\n", File.ReadAllText(Path.Combine(vault, "Novo.md")));
                Assert.Equal("# Dentro\n", File.ReadAllText(Path.Combine(vault, "Pacote", "Dentro.md")));
                Assert.Equal("# Comece\n\n[[Um]]\n", File.ReadAllText(Path.Combine(vault, "Comece.md")));   // different content: kept
                Assert.False(File.Exists(Path.Combine(vault, "foto.bin")));
                Assert.False(Directory.Exists(Path.Combine(vault, ".obsidian")));
                Assert.Equal(notes + 2, view.Documents.List().Count);
                Assert.Contains("Pacote", view.Library!.Folders);
                Assert.StartsWith("Importação concluída. 2 arquivo(s) gravado(s); 1 preservado(s)", view.NoticeText);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Import_RefusesUnsafePathsAndBackupsWithoutTouchingTheVault() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().ToArray();
                foreach (var selection in new[] { Files(false, ("../fora.md", "x")), Files(true, (".urbe/vault.json", "{\"formatVersion\":99}"), ("n.md", "x")) })
                {
                    view.ImportPicker = _ => Task.FromResult<ImportSelection?>(selection);
                    await PickFromNotesMenu(view, "Importar arquivos", async () =>
                    {
                        ClickButtonWithText(view.Dialogs, "Arquivos");
                        await Task.Delay(50);
                        Dispatcher.UIThread.RunJobs();
                        Assert.Contains("Não foi possível importar", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                        ClickButtonWithText(view.Dialogs, "Ok");
                    });
                }
                Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().ToArray());
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Import_RestoresAVaultFolderWithABackupOfWhatItReplaces() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                await UntilNotesSaved(view);
                // the copy of the vault as it is now (what "Exportar"/a copied folder gives)
                var copy = Directory.GetFiles(vault, "*", SearchOption.AllDirectories)
                    .Select(f => (Path: Path.GetRelativePath(vault, f).Replace('\\', '/'), Bytes: File.ReadAllBytes(f))).ToList();
                Assert.Contains(copy, f => f.Path == NoteLibrary.Path);
                // the vault changes afterwards
                File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Mudou\n");
                File.WriteAllText(Path.Combine(vault, "Extra.md"), "# Extra\n");
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.ImportPicker = _ => Task.FromResult<ImportSelection?>(new ImportSelection(
                    copy.Select(f => new ImportSource(f.Path, _ => ValueTask.FromResult<Stream>(new MemoryStream(f.Bytes)))).ToList(), true));
                await PickFromNotesMenu(view, "Importar arquivos", async () =>
                {
                    ClickButtonWithText(view.Dialogs, "Pasta inteira");
                    await Task.Delay(80);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Contains("Vault para restauração", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                    ClickButtonWithText(view.Dialogs, "Continuar");
                    await Task.Delay(150);
                    Dispatcher.UIThread.RunJobs();
                    Assert.Contains("Restaurar este vault?", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                    Snapshot(window, "native-import-restore.png");
                    ClickButtonWithText(view.Dialogs, "Restaurar com backup");
                    await Task.Delay(400);
                    Dispatcher.UIThread.RunJobs();
                });
                Assert.Equal("# Comece\n\n[[Um]]\n", File.ReadAllText(Path.Combine(vault, "Comece.md")));
                Assert.False(File.Exists(Path.Combine(vault, "Extra.md")));
                Assert.False(File.Exists(Path.Combine(vault, ".urbe", "import.v1.json")));
                var backup = Directory.GetDirectories(Path.Combine(vault, ".urbe", "backup"), "import-*").Single();
                Assert.Equal("# Mudou\n", File.ReadAllText(Path.Combine(backup, "files", "Comece.md")));
                Assert.Equal("# Extra\n", File.ReadAllText(Path.Combine(backup, "files", "Extra.md")));
                Assert.StartsWith("Vault restaurado. ", view.Notices[^1]);
                Assert.Null(view.Documents!.Get("Extra.md"));

                // Backups do vault: the import's own backup brings the replaced vault back (and is itself backed up first)
                await RunWithDialog(view.ShowBackupsAsync(), async () =>
                {
                    // Comece.md, Extra.md and the notes' record (and .urbe/identity.json when it was already written)
                    var label = view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Single(t => t?.StartsWith("Antes de importar · ", StringComparison.Ordinal) == true)!;
                    Snapshot(window, "native-backups-list.png");
                    ClickButtonWithText(view.Dialogs, label);
                    await Task.Delay(120);
                    Dispatcher.UIThread.RunJobs();
                    ClickButtonWithText(view.Dialogs, "Continuar");
                    await Task.Delay(150);
                    Dispatcher.UIThread.RunJobs();
                    ClickButtonWithText(view.Dialogs, "Restaurar com backup");
                    await Task.Delay(400);
                    Dispatcher.UIThread.RunJobs();
                });
                Assert.Equal("# Mudou\n", File.ReadAllText(Path.Combine(vault, "Comece.md")));
                Assert.Equal("# Extra\n", File.ReadAllText(Path.Combine(vault, "Extra.md")));
                Assert.Equal(2, Directory.GetDirectories(Path.Combine(vault, ".urbe", "backup"), "import-*").Length);
                Assert.StartsWith("Vault restaurado. ", view.Notices[^1]);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Identity_ANoteRenamedOutsideTheAppKeepsItsId() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                await UntilNotesSaved(view);
                // the record's save also writes the identity sidecar (identity.js), so a later open can recognize renames
                Assert.True(File.Exists(Path.Combine(vault, ".urbe", "identity.json")));
                var before = view.Documents!.Get("Bairro/Um.md")!.Id;
                File.Move(Path.Combine(vault, "Bairro", "Um.md"), Path.Combine(vault, "Bairro", "Um renomeado.md"));
                await view.OpenVaultAsync();
                await Settle(window, view);
                Assert.Equal(before, view.Documents!.Get("Bairro/Um renomeado.md")!.Id);
                Assert.Contains("Uma nota renomeada fora do app foi reconhecida: Bairro/Um renomeado.md.", view.Notices);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Workspace_CleanOrphansForgetsOldHistoriesAndRecordsItAndDiagnosticsReports() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-gc-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, ".urbe"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
            long old = DateTimeOffset.UtcNow.AddDays(-45).ToUnixTimeMilliseconds(), recent = DateTimeOffset.UtcNow.AddDays(-2).ToUnixTimeMilliseconds();
            File.WriteAllText(Path.Combine(vault, ".urbe", "history.v2.json"),
                "{\"version\":2,\"documents\":{\"doc_velho\":[{\"timestamp\":" + old + ",\"path\":\"Sumiu.md\",\"content\":\"x\"}]," +
                "\"doc_recente\":[{\"timestamp\":" + recent + ",\"path\":\"Outra.md\",\"content\":\"y\"}]}}");
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                await RunWithDialog(view.CleanOrphansAsync(), async () =>
                {
                    var texts = view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                    Assert.Contains("Limpar referências órfãs?", texts);
                    Assert.Contains(texts, t => t?.StartsWith("Encontrei 1 histórico de uma nota que não existe mais há mais de 30 dias.", StringComparison.Ordinal) == true);
                    ClickButtonWithText(view.Dialogs, "Limpar");
                    await Task.Delay(200);
                    Dispatcher.UIThread.RunJobs();
                });
                var history = File.ReadAllText(Path.Combine(vault, ".urbe", "history.v2.json"));
                Assert.DoesNotContain("doc_velho", history);
                Assert.Contains("doc_recente", history);
                var meta = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(vault, ".urbe", "vault.json")))!;
                var record = meta["maintenance"]!.AsArray()[^1]!;
                Assert.Equal("gc", (string)record["kind"]!);
                Assert.Equal(1, (int)record["removed"]!["history"]!);
                Assert.Contains("Limpeza feita e registrada no vault.", view.Notices);
                await RunWithDialog(view.ShowDiagnosticsAsync(), async () =>
                {
                    var texts = view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                    Assert.Contains("Tudo certo", texts);
                    Assert.Contains(texts, t => t?.Contains("Notas e arquivos: 1", StringComparison.Ordinal) == true && t.Contains("Pastas:", StringComparison.Ordinal));
                    Snapshot(window, "native-workspace-diagnostics.png");
                    ClickButtonWithText(view.Dialogs, "Ok");
                    await Task.CompletedTask;
                });
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Import_AnExportRestoresAndOffersItsPreferences() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var device = new MemoryDeviceStore();
                device.Set("urbe.tip.link", "1");
                device.Set("urbe.ia.key", "segredo");
                view.Customization.Device = device;
                byte[]? zip = null;
                view.SaveExport = b => { zip = b; return Task.FromResult<bool?>(true); };
                await view.ExportAsync();
                Assert.NotNull(zip);
                var manifest = System.Text.Encoding.UTF8.GetString(zip!);
                Assert.DoesNotContain("segredo", manifest);

                File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Mudou\n");
                await view.OpenVaultAsync();
                await Settle(window, view);
                var fresh = new MemoryDeviceStore();
                view.Customization.Device = fresh;
                view.ImportPicker = _ => Task.FromResult<ImportSelection?>(new ImportSelection([new ImportSource("Urbe-vault.zip", _ => ValueTask.FromResult<Stream>(new MemoryStream(zip!)))]));
                await PickFromNotesMenu(view, "Importar arquivos", async () =>
                {
                    ClickButtonWithText(view.Dialogs, "Arquivos");
                    await Pump(120);
                    ClickButtonWithText(view.Dialogs, "Continuar");
                    await Pump(200);
                    ClickButtonWithText(view.Dialogs, "Restaurar com backup");
                    for (int i = 0; i < 100 && !view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Preferências do backup"); i++) await Pump(40);
                    ClickButtonWithText(view.Dialogs, "Aplicar preferências");
                    await Pump(200);
                });
                Assert.Equal("# Comece\n\n[[Um]]\n", File.ReadAllText(Path.Combine(vault, "Comece.md")));
                Assert.Equal("1", fresh.Get("urbe.tip.link"));
                Assert.Null(fresh.Get("urbe.ia.key"));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
