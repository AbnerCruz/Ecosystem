using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private static byte[] StudyPdf()
    {
        var pdf = new PdfDocumentBuilder();
        var font = pdf.AddStandard14Font(Standard14Font.Helvetica);
        pdf.AddPage(PageSize.A4).AddText("First chapter.", 12, new PdfPoint(30, 700), font);
        pdf.AddPage(PageSize.A4).AddText("Exact quotation from page two.", 12, new PdfPoint(30, 700), font);
        pdf.AddPage(PageSize.A4);
        return pdf.Build();
    }

    [Fact]
    public async Task StudyPdf_SearchesSelectsQuotesSavesAndReturnsToExactRevision() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                File.WriteAllBytes(Path.Combine(vault, "study.pdf"), StudyPdf());
                await view.OpenVaultAsync();
                view.OpenNoteByPath("Comece.md");
                view.Editor.SetMode(EditorMode.Fonte);
                var original = view.Editor.Editor.Text!;
                view.Editor.Editor.SelectionStart = 0;
                view.Editor.Editor.SelectionEnd = original.Length;
                await view.FilePreview.OpenAsync("study.pdf");
                ClickButtonWithText(view.FilePreview, "Texto selecionável");
                for (int i = 0; i < 100 && !view.FilePreview.GetLogicalDescendants().OfType<PdfStudyView>().Any(); i++)
                { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var reader = view.FilePreview.GetLogicalDescendants().OfType<PdfStudyView>().Single();
                for (int i = 0; i < 100 && !reader.ShownText.Contains("First chapter", StringComparison.Ordinal); i++)
                { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Contains("First chapter", reader.ShownText);
                await reader.SearchAsync("Exact quotation");
                Assert.Contains(reader.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? t.Inlines?.Text ?? "").StartsWith("p. 2", StringComparison.Ordinal));
                await reader.GoToPageAsync(3);
                Assert.Equal("", reader.ShownText);
                Assert.Contains(reader.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("não tem texto", StringComparison.Ordinal));
                await reader.GoToPageAsync(2);
                window.Width = 390;
                window.Height = 820;
                await Settle(window, view);
                Snapshot(window, "native-study-pdf-text.png");
                var text = reader.GetLogicalDescendants().OfType<TextBox>().Single(t => t.IsReadOnly);
                text.SelectionStart = 0; text.SelectionEnd = text.Text!.Length;
                ClickButtonWithText(reader, "Adicionar seleção à nota");
                for (int i = 0; i < 40 && !view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(view.Dialogs, "Adicionar em Comece.md");
                for (int i = 0; i < 100 && !view.Notices.Any(n => n.StartsWith("Citação salva em ", StringComparison.Ordinal)); i++)
                { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Contains(view.Notices, n => n.StartsWith("Citação salva em ", StringComparison.Ordinal));
                var saved = File.ReadAllText(Path.Combine(vault, "Comece.md"));
                Assert.Contains("> Exact quotation from page two.", saved);
                Assert.StartsWith(original, saved);
                Assert.Contains("study.pdf#page=2&sha256=", saved);
                var target = saved.Split("[[study.pdf#", StringSplitOptions.None)[1].Split('|')[0];
                await view.Editor.FollowLinkAsync("study.pdf#" + target);
                var restored = view.FilePreview.GetLogicalDescendants().OfType<PdfStudyView>().Single();
                await Settle(window, view);
                Assert.Equal(2, restored.CurrentPage);
                Assert.Contains("Exact quotation from page two.",restored.ShownText);
                File.AppendAllText(Path.Combine(vault, "study.pdf"), "\n% changed revision\n");
                await view.Editor.FollowLinkAsync("study.pdf#" + target);
                Assert.DoesNotContain(view.FilePreview.GetLogicalDescendants(), c => c is PdfStudyView);
                Assert.Contains(view.FilePreview.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Fonte alterada");
                return true;
            }
            finally { window.Close(); Directory.Delete(dir, true); }
        }, CancellationToken.None));

    [Fact]
    public async Task ReferencePanel_OpensEmptyAndAcceptsAnyExplicitFile() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                view.OpenNoteByPath("Comece.md");
                var panel = view.Editor.Pinned;
                panel.Open();
                Assert.True(panel.IsVisible);
                Assert.Null(panel.SourcePath);
                Assert.Null(panel.Text);
                Assert.False(panel.Preview.IsVisible);
                await Settle(window, view);
                Snapshot(window, "native-reference-empty.png");
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBox>(), t => t.PlaceholderText == "Buscar arquivo pelo nome ou caminho");

                File.WriteAllBytes(Path.Combine(vault, "desconhecido.xyz"), [0, 1, 2]);
                await panel.OpenFileAsync("desconhecido.xyz");
                Assert.Equal("desconhecido.xyz", panel.SourcePath);
                Assert.True(panel.Preview.IsVisible);
                Assert.Equal("desconhecido.xyz", panel.Preview.Current);
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Sem visualização nativa para este formato.");
                Assert.False(view.OpenSplit("desconhecido.xyz")); // not in this vault snapshot yet

                using (var png = File.Create(Path.Combine(vault, "drawing.png")))
                using (var bitmap = Pixels.ToBitmap(Enumerable.Repeat((byte)200, 4 * 4 * 4).ToArray(), 4, 4))
                    bitmap.Save(png, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                await view.OpenVaultAsync();
                view.OpenNoteByPath("Comece.md");
                Assert.True(view.OpenSplit("desconhecido.xyz"));
                for (int i = 0; i < 50 && view.Editor.Split.Preview.Current != "desconhecido.xyz"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("desconhecido.xyz", view.Editor.Split.Path);
                view.CloseSplit();
                await panel.OpenFileAsync("drawing.png");
                Assert.Equal("image", panel.Preview.Shown);

                view.OpenNoteByPath("Bairro/Um.md");
                Assert.Equal("drawing.png", panel.SourcePath);
                await panel.OpenFileAsync("Comece.md");
                Assert.Equal("Comece.md", panel.SourcePath);
                Assert.NotNull(panel.Text);
                Assert.False(panel.Preview.IsVisible);
                Assert.True(await panel.TryUnpinAsync());
                Assert.Null(panel.SourcePath);
                panel.Open();
                Assert.Null(panel.SourcePath);
                Assert.Null(panel.Text);
                return true;
            }
            finally { window.Close(); Directory.Delete(dir, true); }
        }, CancellationToken.None));
}
