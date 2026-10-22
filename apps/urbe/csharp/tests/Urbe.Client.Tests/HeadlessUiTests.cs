using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        // tests assert final states and sample frames: the interface's motion is off, as with "Remover animações" (UC-69);
        // the motion tests ask for it explicitly
        UrbeTokens.Motion.Reduced = true;
        return AppBuilder.Configure<UrbeApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}

/// <summary>
/// Drives the real native UI headlessly (Skia rendering, real input routing).
/// Frames are saved to URBE_SNAPSHOT_DIR when set, as visual evidence for review.
/// This is not device validation (NN-017): soft keyboard/IME on Android is G-N0 DEVICE.
/// </summary>
public sealed partial class HeadlessUiTests : IDisposable
{
    // One application for the whole run: per-test isolation re-ran AppBuilder setup on every
    // dispatch and intermittently built the compositor off the dispatcher thread (CI flake).
    internal static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp), AvaloniaTestIsolationLevel.PerAssembly);

    public void Dispose() { }

    [Fact]
    public async Task App_OpensOnTheAreaDeTrabalhoWithTheExplorerBesideIt() => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            Snapshot(window, "native-desktop-explorer.png");

            // DEC-0053: the Área de trabalho is the base; on a wide window the Explorer is a sidebar beside it
            Assert.True(view.SidebarDocked);
            Assert.True(view.Explorer.IsVisible);
            Assert.Equal(MainView.SidebarWidth, view.Explorer.Bounds.Width);
            Assert.True(view.Workspace.IsVisible);
            Assert.True(view.Workspace.Bounds.Width > 800 && view.Workspace.Bounds.Height > 700);
            Assert.False(view.Workspace.ShowsSidebarButton);
            Assert.Equal("Explorer", view.Explorer.Title);
            Assert.Equal("Nenhuma pasta aberta", view.Explorer.Status);
            Assert.Contains(view.Explorer.HeaderActions.Children.OfType<Button>(), b => ToolTip.GetTip(b) as string == "Configurações");
            Assert.Contains(view.Workspace.Index.Projects, p => p.Folder == "Tutorial");

            // the Tutorial's box opens into its folder; a card opens the note in the editor, over both
            ClickButtonWithText(view.Workspace, "Tutorial");
            Assert.Equal("Tutorial", view.Workspace.Folder);
            var card = view.Workspace.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == view.Workspace.Index.Items("Tutorial").First(i => i.Path == "Tutorial/Comece aqui.md").Title);
            card.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settle(window, view);
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Tutorial/Comece aqui.md", view.Editor.Current!.Path);
            Assert.Equal(EditorMode.Visual, view.Editor.Mode);   // 1.8.4 opens notes in Visual (editor.modoInicial)
            Snapshot(window, "native-desktop-editor-visual.png");
            view.Editor.SetMode(EditorMode.Fonte);
            await Settle(window, view);
            Assert.True(view.Editor.Editor.IsFocused);
            view.Editor.Editor.CaretIndex = 0;
            window.KeyTextInput("Olá, ação! ");
            Dispatcher.UIThread.RunJobs();
            Assert.StartsWith("Olá, ação! ", view.Editor.Editor.Text);
            Snapshot(window, "native-desktop-editor.png");

            // leaving the editor shows the Área de trabalho again, where it was, and the sidebar
            view.Editor.IsVisible = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Explorer.IsVisible);
            Assert.Equal("Tutorial", view.Workspace.Folder);
            Assert.True(view.Explorer.TryEnterFolder("Tutorial"));
            Assert.Contains("Tutorial/Comece aqui.md", view.Explorer.VisibleNotePaths);
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task Phone_UsesTheBottomDockWithNotasAndAssistente() => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            Snapshot(window, "native-phone-explorer.png");
            // DEC-0053: on a phone the Área de trabalho fills the screen; the Explorer is a drawer (Pastas)
            Assert.True(view.Workspace.Bounds.Width >= 400);
            Assert.False(view.Explorer.IsVisible);
            Assert.True(view.Workspace.ShowsSidebarButton);
            var dock = view.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Text is "Notas" or "Assistente" or "Cidade" && t.IsEffectivelyVisible && t.FindAncestorOfType<WorkspaceView>() is null).Select(t => t.Text).ToList();
            Assert.Equal(["Notas", "Assistente"], dock);
            Assert.Equal("Notas", view.ActiveDestination);
            ClickNamed(view.Workspace, "Pastas");
            await Settle(window, view);
            Assert.True(view.Explorer.IsVisible && view.Explorer.Overlaid);
            Assert.True(view.Explorer.Bounds.Width >= 400);
            Snapshot(window, "native-phone-drawer.png");
            // Fechar (or a note chosen) closes the drawer
            view.Explorer.GetLogicalDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == "Fechar").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settle(window, view);
            Assert.False(view.Explorer.IsVisible);
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task Editor_OnlyConfirmsSavedAfterVaultHostAcknowledges() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var editor = new EditorOverlay { SaveOnClose = true };
            var window = new Window { Width = 800, Height = 500, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Exemplo.md", "original"));
            editor.Editor.Text = "texto novo";
            Assert.True(editor.HasChanges);
            CityNote? written = null;
            editor.SaveRequested += note => { written = note; return Task.CompletedTask; };
            Assert.True(await editor.SaveAsync());
            Assert.Equal("texto novo", written?.Content);
            Assert.False(editor.HasChanges);

            var failed = new EditorOverlay { SaveOnClose = true };
            failed.Open(new CityNote("Notas/Exemplo.md", "original"));
            failed.Editor.Text = "não salvar";
            failed.SaveRequested += _ => throw new IOException("sem permissão");
            Assert.False(await failed.SaveAsync());
            Assert.True(failed.HasChanges);
            return true;
        }, CancellationToken.None));


    [Fact]
    public async Task NativeExplorer_ListsFoldersSearchesAndOpensSelectedNote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            view.ShowNotes();
            Assert.True(view.Explorer.IsVisible);
            Assert.Contains("Tutorial", view.Explorer.VisibleFolderPaths);
            Assert.True(view.Explorer.TryEnterFolder("Tutorial"));
            Assert.True(view.Explorer.IsExpanded("Tutorial"));
            Assert.Contains("Tutorial/Comece aqui.md", view.Explorer.VisibleNotePaths);
            view.Explorer.SearchText = "Comece";
            Assert.Contains("Tutorial/Comece aqui.md", view.Explorer.VisibleNotePaths);
            Assert.True(view.Explorer.TryOpenNote("Tutorial/Comece aqui.md"));
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Tutorial/Comece aqui.md", view.Editor.Current?.Path);
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task NativeExplorer_PreventsOpeningInvisibleOrUnknownPaths() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var explorer = new NotesExplorer();
            var window = new Window { Width = 410, Height = 760, Content = explorer };
            window.Show();
            explorer.SetNotes([
                new CityNote("Raiz.md", "# raiz"),
                new CityNote("Bairro/Um.md", "# um"),
                new CityNote("Bairro/Sub/Dois.md", "# dois")
            ]);
            Assert.Equal(new[] { "Raiz.md" }, explorer.VisibleNotePaths);
            Assert.Equal(new[] { "Bairro" }, explorer.VisibleFolderPaths);
            Assert.False(explorer.TryOpenNote("Bairro/Um.md"));
            Assert.True(explorer.TryEnterFolder("Bairro"));
            Assert.Contains("Bairro/Um.md", explorer.VisibleNotePaths);
            Assert.Contains("Bairro/Sub", explorer.VisibleFolderPaths);
            Assert.False(explorer.TryEnterFolder("Bairro/Sub/ausente"));
            explorer.SearchText = "Dois";
            Assert.Contains("Bairro/Sub/Dois.md", explorer.VisibleNotePaths);
            Assert.True(explorer.TryOpenNote("Bairro/Sub/Dois.md"));
            return true;
        }, CancellationToken.None));

    /// <summary>
    /// ADD-0028, the safety of the person's data: a vault opened by the city keeps the ids and dates its .urbe/mapa.json
    /// gave its notes (history, trash and pages depend on the ids), now recorded in .urbe/notas.json; the city files
    /// (.urbe/mapa.json, .urbe/mundo.json) are never rewritten, not at the opening, not after creating or renaming a note,
    /// and a new app on the same folder finds the same ids.
    /// </summary>
    [Fact]
    public async Task Vault_OpenedByTheCityKeepsItsIdsAndDatesAndTheCityFilesAreNeverRewritten() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-city-vault-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Projetos", "Vazia"));
            Directory.CreateDirectory(Path.Combine(vault, ".urbe"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Plano]]\n");
            File.WriteAllText(Path.Combine(vault, "Projetos", "Plano.md"), "# Plano\n");
            File.WriteAllBytes(Path.Combine(vault, "foto.png"), [1, 2, 3]);
            var mapa = """
                {
                 "v": 4,
                 "mundo": "placas-1",
                 "camera": {"x": 10, "y": 20, "z": 1.2},
                 "regioes": [{"id": "reg_p", "caminho": "Projetos", "nome": "Projetos", "x": 1, "y": 1, "w": 9, "h": 9}, {"id": "reg_v", "caminho": "Projetos/Vazia", "nome": "Vazia", "x": 2, "y": 2, "w": 3, "h": 3}],
                 "notas": {"Comece.md": {"id": "doc_comece", "x": 5, "y": 5, "sprite": "house2", "tags": [], "criado": "2025-02-03", "modificado": "2025-04-05"},
                           "Projetos/Plano.md": {"id": "doc_plano", "x": 3, "y": 3, "criado": "2025-01-01", "modificado": "2025-01-02"}},
                 "construcoes": []
                }
                """;
            var mundo = """{"versao":1,"muralha":true,"decoracoes":[{"tipo":"poco","x":4,"y":4}]}""";
            File.WriteAllText(Path.Combine(vault, ".urbe", "mapa.json"), mapa);
            File.WriteAllText(Path.Combine(vault, ".urbe", "mundo.json"), mundo);
            void CityFilesUntouched()
            {
                Assert.Equal(mapa, File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json")));
                Assert.Equal(mundo, File.ReadAllText(Path.Combine(vault, ".urbe", "mundo.json")));
            }
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                Assert.Equal("vault", view.Explorer.Title);
                Assert.Equal(("doc_comece", "2025-02-03", "2025-04-05"), (view.Documents!.Get("Comece.md")!.Id, view.Documents.Get("Comece.md")!.Created, view.Documents.Get("Comece.md")!.Modified));
                Assert.Equal("doc_plano", view.Documents.Get("Projetos/Plano.md")!.Id);
                // the empty folder of the old neighbourhood is still a folder
                Assert.Contains("Projetos/Vazia", view.Library!.Folders);
                Assert.Contains("foto.png", view.Explorer.VisibleFilePaths);

                // the first save records them in .urbe/notas.json; the city files stay byte for byte
                await UntilNotesSaved(view);
                var record = File.ReadAllText(Path.Combine(vault, NoteLibrary.Path));
                Assert.Contains("\"doc_comece\"", record);
                Assert.Contains("\"Projetos/Vazia\"", record);
                CityFilesUntouched();

                // a new note and a rename: the files and the record change, the city files never
                var create = view.CreateNoteAsync("", "Ideias");
                Dispatcher.UIThread.RunJobs();
                await create;
                view.Editor.IsVisible = false;
                await view.RenameNoteAsync("Projetos/Plano.md", "Roteiro");
                await UntilNotesSaved(view);
                Assert.True(File.Exists(Path.Combine(vault, "Ideias.md")));
                Assert.True(File.Exists(Path.Combine(vault, "Projetos", "Roteiro.md")));
                Assert.Equal("doc_plano", view.Documents.Get("Projetos/Roteiro.md")!.Id);
                CityFilesUntouched();
                Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(vault, "foto.png")));
                var ideias = view.Documents.Get("Ideias.md")!.Id;
                window.Close();

                // a new app on the same folder: the same ids, from the record now (the map still says Plano.md)
                var reopened = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app2", "sel.json")));
                var window2 = new Window { Width = 1280, Height = 760, Content = reopened };
                window2.Show();
                await reopened.OpenVaultAsync();
                await Settle(window2, reopened);
                Assert.Equal("doc_plano", reopened.Documents!.Get("Projetos/Roteiro.md")!.Id);
                Assert.Equal("doc_comece", reopened.Documents.Get("Comece.md")!.Id);
                Assert.Equal(ideias, reopened.Documents.Get("Ideias.md")!.Id);
                Assert.Equal("2025-02-03", reopened.Documents.Get("Comece.md")!.Created);
                await UntilNotesSaved(reopened);
                CityFilesUntouched();
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    private sealed class SnapshotStorage(VaultSnapshot snapshot) : IUrbeVaultStorage
    {
        public bool IsConnected => true;
        public Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default) => Task.FromResult<VaultSnapshot?>(snapshot);
        public Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default) => Task.FromResult<VaultSnapshot?>(snapshot);
        public Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read-only test storage");
        public Task CreateTextFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read-only test storage");
        public Task SaveExistingTextFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read-only test storage");
    }

    /// <summary>A vault that takes its time to be read (a phone's folder): RestoreAsync waits for <see cref="Release"/>.</summary>
    private sealed class SlowStorage(VaultSnapshot snapshot) : IUrbeVaultStorage
    {
        private readonly TaskCompletionSource _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release() => _read.TrySetResult();
        public bool IsConnected => false;
        public Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default) => Task.FromResult<VaultSnapshot?>(snapshot);
        public async Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default) { await _read.Task; return snapshot; }
        public Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read-only test storage");
        public Task CreateTextFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read-only test storage");
        public Task SaveExistingTextFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("read-only test storage");
    }

    [Fact]
    public async Task Opening_WhileTheVaultIsReadTheExplorerSaysSoNotThatThereAreNoNotes() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // observation of 22/10/2026: the opening showed "no notes", as if there were no vault
            var storage = new SlowStorage(VaultReader.Read([new VaultFile("Comece.md", System.Text.Encoding.UTF8.GetBytes("# Comece\n"))]));
            var view = new MainView(storage);
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            var init = view.InitializeAsync();
            Dispatcher.UIThread.RunJobs();
            // the base (the Área de trabalho, DEC-0053) says it is opening, not that there is nothing
            var texts = view.Workspace.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Abrindo suas notas…", texts);
            Assert.DoesNotContain("Novo projeto", texts);
            view.ShowNotes();
            Dispatcher.UIThread.RunJobs();
            texts = view.Explorer.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Abrindo suas notas…", texts);
            Assert.DoesNotContain("Nenhuma nota ainda", texts);
            storage.Release();
            await init;
            Dispatcher.UIThread.RunJobs();
            texts = view.Explorer.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.DoesNotContain("Abrindo suas notas…", texts);
            Assert.Contains("Comece.md", texts);
            Assert.Contains("Comece", view.Workspace.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task NewNote_IsWrittenToTheVaultAndEdited() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-new-note-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Um]]\n");
            File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
            try
            {
                var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
                var view = new MainView(storage);
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                int notes = view.Documents!.List().Count;

                var menu = view.CreateNoteAsync(null);
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(50);
                Dispatcher.UIThread.RunJobs();
                var input = view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single();
                input.Text = "Ideias";
                Snapshot(window, "native-new-note-dialog.png");
                ClickButtonWithText(view.Dialogs, "Criar nota");
                await menu;

                var path = Path.Combine(vault, "Ideias.md");
                Assert.Equal("# ", File.ReadAllText(path));
                Assert.Equal(notes + 1, view.Documents.List().Count);
                Assert.True(view.Editor.IsVisible);
                Assert.Equal("# ", view.Editor.Editor.Text);
                Assert.Contains("Ideias.md", view.Explorer.VisibleNotePaths);

                // the new note is an ordinary vault file from now on
                view.Editor.Editor.Text = "# Ideias\n\nPrimeira.\n";
                Assert.True(await view.Editor.SaveAsync());
                Assert.Equal("# Ideias\n\nPrimeira.\n", File.ReadAllText(path));

                // a second note with the same name in the same place gets " (2)", never overwriting
                view.Editor.IsVisible = false;
                var again = view.CreateNoteAsync(null);
                Dispatcher.UIThread.RunJobs();
                view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "Ideias";
                ClickButtonWithText(view.Dialogs, "Criar nota");
                await again;
                Assert.Equal("# ", File.ReadAllText(Path.Combine(vault, "Ideias (2).md")));
                Assert.Equal("# Ideias\n\nPrimeira.\n", File.ReadAllText(path));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task NewNote_WithoutAWritableVaultIsNotCreatedInMemory() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            int notes = view.Documents!.List().Count;
            var create = view.CreateNoteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Dialogs.IsOpen);
            Assert.Empty(view.Dialogs.GetVisualDescendants().OfType<TextBox>());   // an explanation, not a name field
            Snapshot(window, "native-new-note-readonly.png");
            ClickButtonWithText(view.Dialogs, "Ok");
            await create;
            Assert.Equal(notes, view.Documents.List().Count);
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task Search_QuickOpenAndCommandPaletteWorkLikeTheOriginal() => Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);

            // Buscar notas; with nothing typed it lists the recent notes
            view.Search.OpenQuickOpen();
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Search.QuickOpen.IsOpen);
            Assert.True(view.Search.QuickOpen.RowCount > 0);
            Assert.Contains(view.Search.QuickOpen.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "RECENTES");
            Snapshot(window, "native-quick-open.png");

            // typing ranks (no accents, no case); the arrows move; Enter opens the note
            view.Search.QuickOpen.Input.Text = "COMECE";
            Dispatcher.UIThread.RunJobs();
            var docs = view.Search.QuickOpen.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("Comece aqui", docs);
            Assert.Contains(docs, t => t?.StartsWith("Criar nota “COMECE”") == true);
            Assert.Equal(0, view.Search.QuickOpen.ActiveIndex);
            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
            Assert.Equal(Math.Min(1, view.Search.QuickOpen.RowCount - 1), view.Search.QuickOpen.ActiveIndex);
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            Assert.Equal(0, view.Search.QuickOpen.ActiveIndex);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.Search.QuickOpen.IsOpen);
            Assert.True(view.Editor.IsVisible);
            Assert.Equal("Tutorial/Comece aqui.md", view.Editor.Current!.Path);
            view.Editor.IsVisible = false;

            // keymap.js: Ctrl+K opens the palette, which filters the commands; Esc closes
            window.KeyPress(Key.K, RawInputModifiers.Control, PhysicalKey.K, "k");
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Search.Palette.IsOpen);
            view.Search.Palette.Input.Text = "tema.json";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, view.Search.Palette.RowCount);
            Snapshot(window, "native-command-palette.png");
            view.Search.Palette.Input.Text = "cidade";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, view.Search.Palette.RowCount);   // no command of the city is left (ADD-0028)
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.False(view.Search.Palette.IsOpen);

            // Ctrl+P opens Buscar notas; a name that does not exist offers "Criar nota"
            window.KeyPress(Key.P, RawInputModifiers.Control, PhysicalKey.P, "p");
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Search.QuickOpen.IsOpen);
            view.Search.QuickOpen.Input.Text = "zzz inexistente";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, view.Search.QuickOpen.RowCount);
            Assert.Contains(view.Search.QuickOpen.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Na raiz · uma nota nova no vault");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.False(view.Search.QuickOpen.IsOpen);
            window.Close();
            return true;
        }, CancellationToken.None));

    /// <summary>Clicks the button named <paramref name="name"/> for screen readers (an icon button).</summary>
    private static void ClickNamed(Control root, string name)
    {
        root.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == name)
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void ClickButtonWithText(Control root, string text)
    {
        var button = root.GetLogicalDescendants().OfType<Button>()
            .First(b => b.Content as string == text || b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == text));
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Lets the layout, the pending jobs and two frames run (the screen as the person would see it).</summary>
    private static async Task Settle(Window window, MainView view)
    {
        for (int i = 0; i < 3; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            await Task.Delay(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Snapshot(Window window, string name)
    {
        var dir = Environment.GetEnvironmentVariable("URBE_SNAPSHOT_DIR");
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame!.Save(Path.Combine(dir, name), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
