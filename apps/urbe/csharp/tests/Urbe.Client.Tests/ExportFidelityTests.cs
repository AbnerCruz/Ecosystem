using System.Diagnostics;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-185: each exported format opened by a real application (LibreOffice for DOCX, ODT and PPTX; poppler for the PDF),
/// and its text read back out — so the words are text that can be edited and searched, never a picture. Skipped where
/// those applications are not installed (the CI's runner).
/// </summary>
public sealed class ExportFidelityTests
{
    private const string Note = "# Capítulo único\n\nAção, coração e pão: acentos que precisam sobreviver.\n\n## Tabela\n\n| Item | Preço |\n|---|---|\n| Café | 5 |\n\n- primeiro ponto\n- segundo ponto\n";

    private static string? Tool(string name) =>
        Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).Select(d => Path.Combine(d, name)).FirstOrDefault(File.Exists);

    private static string Run(string exe, string args, string cwd)
    {
        using var p = Process.Start(new ProcessStartInfo(exe, args) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(120_000);
        return output;
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("odt")]
    [InlineData("pptx")]
    [InlineData("pdf")]
    public async Task TheExportOpensInARealAppAndItsTextIsText(string format)
    {
        if (Tool("pdftotext") is not { } pdftotext || format != "pdf" && Tool("soffice") is null) { Assert.Skip("LibreOffice ou poppler ausente neste ambiente"); return; }
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Capítulo.md", Content = Note });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Prova de fidelidade", note.Id);
        var cancel = TestContext.Current.CancellationToken;
        var export = format switch
        {
            "docx" => await PublicationDocx.ExportAsync(project, catalog, cancellationToken: cancel),
            "odt" => await PublicationOdt.ExportAsync(project, catalog, cancellationToken: cancel),
            "pptx" => await PublicationPptx.ExportAsync(project, catalog, cancellationToken: cancel),
            _ => await PublicationPdf.ExportAsync(project, catalog, cancellationToken: cancel)
        };
        var dir = Directory.CreateTempSubdirectory("urbe-fidelity-").FullName;
        try
        {
            var file = Path.Combine(dir, "prova." + format);
            await File.WriteAllBytesAsync(file, export.Bytes, cancel);
            var pdf = file;
            if (format != "pdf")
            {
                Run(Tool("soffice")!, "--headless --convert-to pdf --outdir \"" + dir + "\" \"" + file + "\"", dir);
                pdf = Path.Combine(dir, "prova.pdf");
                Assert.True(File.Exists(pdf), "o LibreOffice não abriu o " + format.ToUpperInvariant());
            }
            var text = Run(pdftotext, "-layout \"" + pdf + "\" -", dir);
            Assert.Contains("Ação, coração e pão", text);
            Assert.Contains("Café", text);
            Assert.Contains("segundo ponto", text);
        }
        finally { Directory.Delete(dir, true); }
    }
}
