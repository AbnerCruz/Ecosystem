using System.Text;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-207: a ```dados block reads a CSV of the vault (Latin-1 from a Brazilian spreadsheet too) and shows the result.</summary>
    [Fact]
    public async Task DataBlock_ReadsTheVaultsCsv_AndShowsTheSteps() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-data-" + Guid.NewGuid().ToString("N"));
        try
        {
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Planilhas"));
            // a spreadsheet saved as Windows-1252 (Latin-1 for these letters)
            File.WriteAllBytes(Path.Combine(vault, "Planilhas", "gastos.csv"), Encoding.Latin1.GetBytes("categoria;descrição;valor\nMercado;Pão;12,50\nTransporte;Ônibus;4,40\nMercado;Café;30\n"));
            File.WriteAllText(Path.Combine(vault, "Relatório.md"), "# Relatório\n\n```dados\nfonte: Planilhas/gastos.csv\nagrupar por categoria: soma(valor) como total\nordenar por total desc\n```\n");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "sel.json")));
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            Assert.Equal("transporte", (await view.ReadDataSourceAsync("Planilhas/gastos.csv"))!.Split('\n')[2].Split(';')[0].ToLowerInvariant());
            Assert.Contains("Ônibus", await view.ReadDataSourceAsync("Planilhas/gastos.csv"));
            view.OpenNoteByPath("Relatório.md");
            view.Editor.SetMode(EditorMode.Visual);
            await BenchPump(window);
            var block = view.Editor.GetVisualDescendants().OfType<Urbe.Client.Ui.DataBlockView>().Single();
            await block.Reading;
            await BenchPump(window);
            Assert.Null(block.Error);
            var texts = block.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Equal(["categoria", "total", "Mercado", "42,5", "Transporte", "4,4"], texts.Take(6));
            Assert.Contains(texts, t => t!.Contains("fonte: Planilhas/gastos.csv"));
            Snapshot(window, "data-block.png");
            window.Close();
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-208: a ```dados block ending with “gráfico …” shows the chart.</summary>
    [Fact]
    public async Task DataBlock_EndingWithAChart_ShowsTheChart() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-chart-" + Guid.NewGuid().ToString("N"));
        try
        {
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Meses.md"), "# Meses\n\n| mês | receita |\n|---|---:|\n| Jan | 1200 |\n| Fev | 1500 |\n\n```dados\nfonte: Meses.md\ngráfico barras mês receita\n```\n");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "sel.json")));
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.OpenNoteByPath("Meses.md");
            view.Editor.SetMode(EditorMode.Visual);
            await BenchPump(window);
            var block = view.Editor.GetVisualDescendants().OfType<Urbe.Client.Ui.DataBlockView>().Single();
            await block.Reading;
            await BenchPump(window);
            Assert.Null(block.Error);
            Assert.NotNull(block.Chart);
            Assert.True(block.Chart!.Bounds.Width > 100);
            Snapshot(window, "data-chart.png");
            window.Close();
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
