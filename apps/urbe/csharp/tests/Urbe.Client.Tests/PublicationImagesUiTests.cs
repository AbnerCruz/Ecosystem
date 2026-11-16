using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-162: the exported HTML carries the vault's images (its policy allows only data: images).</summary>
    [Fact]
    public async Task Publication_HtmlExportEmbedsVaultImages() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubimg-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Imagens"));
        File.WriteAllText(Path.Combine(vault, "Mapa.md"), "# Mapa\n\nVeja:\n\n![[mapa.png]]\n");
        File.WriteAllBytes(Path.Combine(vault, "Imagens", "mapa.png"), Convert.FromBase64String(Png));
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var create = view.CreatePublicationAsync("Mapa.md");
            Dispatcher.UIThread.RunJobs();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Atlas";
            ClickButtonWithText(view.Dialogs, "Criar");
            await create;
            await Settle(window, view);
            string? html = null;
            view.SavePageFile = (_, bytes) => { html = System.Text.Encoding.UTF8.GetString(bytes); return Task.FromResult<bool?>(true); };
            await view.ExportPublicationHtmlAsync();
            Assert.NotNull(html);
            Assert.Contains("<img src=\"data:image/png;base64," + Png + "\" alt=\"mapa\">", html); // found by name in Imagens/
            // the same picture in a Word file
            byte[]? docx = null;
            view.SavePageFile = (_, bytes) => { docx = bytes; return Task.FromResult<bool?>(true); };
            var export = view.ExportPublicationOfficeAsync("docx");
            for (int i = 0; i < 300 && !export.IsCompleted; i++) { if (view.Dialogs.IsOpen) ClickButtonWithText(view.Dialogs, "Ok"); await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            await export;
            Assert.NotNull(docx);
            using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(docx!));
            Assert.Contains(archive.Entries, e => e.FullName.StartsWith("word/media/image", StringComparison.Ordinal));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    /// <summary>CST-153: saving a note that feeds a publication names it, once per session.</summary>
    [Fact]
    public async Task Publication_SavingASourceNamesTheAffectedPublication() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubaffected-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Fonte.md"), "# Fonte\n\nTexto.\n\n## Parte dois\n\nMais.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var create = view.CreatePublicationAsync("Fonte.md");
            Dispatcher.UIThread.RunJobs();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Livro";
            ClickButtonWithText(view.Dialogs, "Criar");
            await create;
            await Settle(window, view);
            view.OpenNoteByPath("Fonte.md");
            await Settle(window, view);
            view.Editor.SetMode(EditorMode.Fonte);
            view.Editor.Editor.Text = "# Fonte\n\nTexto novo.\n";
            Assert.True(await view.Editor.SaveAsync());
            Assert.Equal("Esta nota alimenta a publicação Livro. A mudança entra na próxima exportação.", view.Editor.NoticeText);
            // a passage the publication takes is removed: a warning, at once
            await view.FlushPendingAsync(); // the notes' ids are on disk before the vault is read again
            File.WriteAllText(Path.Combine(vault, "Publicações", "Trecho.publication.json"),
                Urbe.Core.PublicationProjectCodec.Serialize(Urbe.Core.PublicationProject.Create("Trecho")
                    .AddSource(new Urbe.Core.PublicationSourceReference(view.Documents!.Get("Fonte.md")!.Id, "Parte dois"))));
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.OpenNoteByPath("Fonte.md");
            await Settle(window, view);
            view.Editor.SetMode(EditorMode.Fonte);
            view.Editor.Editor.Text = "# Fonte\n\nSem a parte dois.\n";
            Assert.True(await view.Editor.SaveAsync());
            Assert.Equal("A publicação Trecho usa o trecho “Parte dois”, que não existe mais nesta nota. Restaure o título ou escolha outro trecho na publicação.", view.NoticeText);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
