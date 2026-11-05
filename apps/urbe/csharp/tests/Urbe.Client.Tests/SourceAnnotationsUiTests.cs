using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-239: a highlight made in the PDF reader is read back from its note, exported as Web Annotation and flagged (never re-anchored) once the PDF changes.</summary>
    [Fact]
    public async Task SourceAnnotations_AreReadFromTheNoteExportedAndFlaggedWhenTheSourceChanges() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            byte[] bytes = StudyPdf(); File.WriteAllBytes(Path.Combine(vault, "visual.pdf"), bytes);
            await view.OpenVaultAsync(); view.OpenNoteByPath("Comece.md");
            await view.FilePreview.OpenPdfReferenceAsync("visual.pdf", 2);
            var pdf = view.FilePreview.GetLogicalDescendants().OfType<PdfPagesView>().Single();
            await Settle(window, view); await pdf.EnableSelectionAsync(); await Settle(window, view);
            var words = DocumentWordGeometry.Read(bytes, 2);
            var selection = pdf.GetLogicalDescendants().OfType<PdfWordSelection>().Single();
            Point Center(DocumentWord word) => new((word.Bounds.X + word.Bounds.Width / 2) * selection.Bounds.Width, (word.Bounds.Y + word.Bounds.Height / 2) * selection.Bounds.Height);
            selection.SelectAt(Center(words[0]), true); selection.SelectAt(Center(words[^1]), false);
            pdf.CiteSelection(StudyAnnotationKind.Highlight); await ReaderWait(() => view.Dialogs.IsOpen); ClickButtonWithText(view.Dialogs, "Adicionar em Comece.md");
            await ReaderWait(() => view.Notices.Any(n => n.StartsWith("Citação salva em", StringComparison.Ordinal)));

            var found = await view.SourceAnnotationsAsync("visual.pdf");
            var (annotation, status) = Assert.Single(found);
            Assert.Equal(SourceAnnotationKind.Highlight, annotation.Kind);
            Assert.Equal("Exact quotation from page two.", annotation.Quote);
            Assert.Equal(("Comece.md", 2), (annotation.NotePath, annotation.Index));
            Assert.Equal(SourceAnnotationStatus.Current, status);

            using (var json = JsonDocument.Parse(await view.SourceAnnotationsJsonAsync("visual.pdf", [annotation])))
            {
                var item = json.RootElement.GetProperty("first").GetProperty("items")[0];
                Assert.Equal("highlighting", item.GetProperty("motivation").GetString());
                Assert.Equal("visual.pdf", item.GetProperty("target").GetProperty("source").GetString());
                Assert.Contains("Exact quotation from page two.", item.GetProperty("target").GetRawText());
            }

            await view.FilePreview.TryCloseAsync();
            await view.OpenAnnotationInNoteAsync(annotation);
            var text = view.Editor.Editor.Text ?? "";
            int lineStart = 0; for (int i = 1; i < annotation.Line; i++) lineStart = text.IndexOf('\n', lineStart) + 1;
            Assert.Equal(lineStart, view.Editor.Editor.CaretIndex);
            Assert.StartsWith("**Destaque**", text[lineStart..]);

            // the PDF changes: the annotation is flagged for review and still points at its own revision
            File.WriteAllBytes(Path.Combine(vault, "visual.pdf"), [.. bytes, .. "\n%changed\n"u8.ToArray()]);
            var after = Assert.Single(await view.SourceAnnotationsAsync("visual.pdf"));
            Assert.Equal(SourceAnnotationStatus.Outdated, after.Status);
            Assert.Equal(annotation.Sha256, after.Annotation.Sha256);
            return true;
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }, CancellationToken.None));
}
