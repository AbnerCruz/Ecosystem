using Urbe.Core.Data;

namespace Urbe.Core.Tests;

/// <summary>CST-207: typed tables from CSV/JSON/notes and reproducible steps that keep the source rows.</summary>
public sealed class DataPipelineTests
{
    private const string Csv = "data;categoria;descrição;valor\n2026-11-01;Mercado;\"Feira; frutas\";R$ 120,50\n2026-11-02;Transporte;Ônibus;4,40\n2026-11-03;Mercado;\"Linha\nquebrada\";1.250,00\n2026-11-05;Lazer;Cinema;35\n2026-11-06;Transporte;;\n";

    private static DataTable Gastos() => DataReaders.Csv("Planilhas/gastos.csv", Csv);

    [Fact]
    public void Csv_DetectsSeparator_QuotesAndTypes()
    {
        var t = Gastos();
        Assert.Equal(["data", "categoria", "descrição", "valor"], t.Columns);
        Assert.Equal([DataType.Date, DataType.Text, DataType.Text, DataType.Number], t.Types);
        Assert.Equal(5, t.Rows.Count);
        Assert.Equal("Feira; frutas", t.Rows[0][2].Text);
        Assert.Equal("Linha\nquebrada", t.Rows[2][2].Text);
        Assert.Equal(120.5, t.Rows[0][3].Number);
        Assert.Equal(1250, t.Rows[2][3].Number);
        Assert.Equal([2, 3, 4, 6, 7], t.SourceRows); // the file's own lines, the quoted break counted
    }

    [Fact]
    public void Steps_FilterSortGroupComputeAndKeepProvenance()
    {
        var r = DataQuery.Run(Gastos(), ["filtrar categoria = \"Mercado\" ou valor > 30", "ordenar por valor desc", "colunas descrição, valor"]);
        Assert.Equal(["descrição", "valor"], r.Table.Columns);
        Assert.Equal(["Linha\nquebrada", "Feira; frutas", "Cinema"], r.Table.Rows.Select(x => x[0].Text));
        Assert.Equal([4, 2, 6], r.Table.SourceRows);

        var g = DataQuery.Run(Gastos(), ["agrupar por categoria: soma(valor) como total, contar", "ordenar por total desc"]).Table;
        Assert.Equal(["categoria", "total", "contagem"], g.Columns);
        Assert.Equal("Mercado", g.Rows[0][0].Text);
        Assert.Equal("1370,5", g.Rows[0][1].Text);
        Assert.Equal("2", g.Rows[0][2].Text);
        Assert.Equal("4,4", g.Rows.Single(x => x[0].Text == "Transporte")[1].Text); // the empty value does not count as zero

        var c = DataQuery.Run(Gastos(), ["calcular com_taxa = valor * 1,1", "limitar 2"]);
        Assert.Equal("132,55", c.Table.Rows[0][4].Text);
        Assert.Equal(2, c.Table.Rows.Count);
        var withEmpty = DataQuery.Run(Gastos(), ["calcular dobro = valor * 2"]);
        Assert.Contains(withEmpty.Notes, n => n.Contains("1 linha(s)"));
    }

    [Fact]
    public void Steps_ExplainMistakes()
    {
        Assert.Contains("Não há coluna", Assert.Throws<InvalidDataException>(() => DataQuery.Run(Gastos(), ["filtrar preco > 1"])).Message);
        Assert.Contains("não é um número", Assert.Throws<InvalidDataException>(() => DataQuery.Run(Gastos(), ["filtrar valor > muito"])).Message);
        Assert.Contains("Etapa desconhecida", Assert.Throws<InvalidDataException>(() => DataQuery.Run(Gastos(), ["pular 3"])).Message);
        Assert.Contains("não é numérica", Assert.Throws<InvalidDataException>(() => DataQuery.Run(Gastos(), ["agrupar por categoria: soma(descrição)"])).Message);
    }

    [Fact]
    public void Json_AndNoteTables_AndTheBlock()
    {
        var j = DataReaders.Json("x.json", """{"itens":[{"nome":"A","qtd":2,"ok":true},{"nome":"B","qtd":3.5}]}""");
        Assert.Equal(["nome", "qtd", "ok"], j.Columns);
        Assert.Equal(DataType.Number, j.Types[1]);
        Assert.Equal(DataType.Boolean, j.Types[2]);
        var note = "# Livros\n\n## Lidos\n\n| Título | Páginas |\n|---|---:|\n| Dom Casmurro | 256 |\n| O Alienista | 96 |\n\n## Quero ler\n\n| Título | Páginas |\n|---|---|\n| Grande Sertão | 624 |\n";
        var lidos = DataReaders.MarkdownTable("Livros.md", note, "Lidos");
        Assert.Equal(2, lidos.Rows.Count);
        Assert.Equal([7, 8], lidos.SourceRows);
        var block = DataBlock.Run("fonte: Livros.md#Quero ler\nresumo", p => p == "Livros.md" ? note : null);
        Assert.Equal("624", block.Table.Rows.Single(r => r[0].Text == "Páginas")[3].Text);
        var csvBlock = DataBlock.Run("fonte: Planilhas/gastos.csv\nfiltrar categoria contém merc\nagrupar por categoria: media(valor)", p => p == "Planilhas/gastos.csv" ? Csv : null);
        Assert.Equal("685,25", csvBlock.Table.Rows[0][1].Text);
        Assert.Contains("Fonte não encontrada", Assert.Throws<InvalidDataException>(() => DataBlock.Run("fonte: nada.csv", _ => null)).Message);
        var md = block.Table.ToMarkdown();
        Assert.StartsWith("| coluna |", md);
        Assert.Contains("\"nome\": \"A\"", j.ToJson());
        Assert.Contains("\"Feira; frutas\"", Gastos().ToCsv());
    }
}

/// <summary>CST-207: the agents' data tool and the block in publications.</summary>
public sealed class DataToolTests
{
    private sealed class Host : Urbe.Core.Assistant.IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public Host() => Knowledge = new KnowledgeIndex(Documents);
        public int ReadChars => 4000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<Urbe.Core.Assistant.AiPlanStep> steps) { }
    }

    [Fact]
    public async Task Tool_QueriesANoteTable_AndReportsSourceRows()
    {
        var host = new Host();
        host.Documents.Upsert(new DocumentInput { Path = "Notas/Livros.md", Content = "# Livros\n\n| Título | Páginas |\n|---|---:|\n| A | 300 |\n| B | 120 |\n| C | 450 |\n" });
        var answer = await Urbe.Core.Assistant.AssistantDataTools.AnswerAsync(host, "Notas/Livros.md", "filtrar Páginas > 200\nordenar por Páginas desc", null, null);
        Assert.Contains("2 linha(s)", answer);
        Assert.Contains("| C | 450 |", answer);
        Assert.Contains("Linhas de origem: 7, 5", answer);
        Assert.StartsWith("Fonte não encontrada", await Urbe.Core.Assistant.AssistantDataTools.AnswerAsync(host, "nada.csv", null, null, null));
        Assert.Contains("ERRO: Não há coluna", await Urbe.Core.Assistant.AssistantDataTools.AnswerAsync(host, "Notas/Livros", "ordenar por autor", null, null));
    }

    [Fact]
    public async Task Publications_RenderTheBlockFromPreloadedSources()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Rel.md", Content = "# Relatório\n\n```dados\nfonte: gastos.csv\nagrupar por cat: soma(v) como total\n```\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Rel", note.Id);
        Func<string, string?> data = p => p == "gastos.csv" ? "cat,v\nA,1\nA,2\nB,5\n" : null;
        Assert.Equal(["gastos.csv"], Urbe.Core.Data.DataBlock.Sources(note.Content));
        var html = PublicationHtml.Export(project, catalog, data).Html;
        Assert.Contains("<table class=\"urbe-data\">", html);
        Assert.Contains("<td style=\"text-align:right\">3</td>", html);
        Assert.Contains("Dados indisponíveis", PublicationHtml.Export(project, catalog).Html);
        var docx = await PublicationDocx.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken, data: data);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(docx.Bytes));
        using var r = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var xml = r.ReadToEnd();
        Assert.Contains(">total<", xml);
        Assert.Contains("Fonte: gastos.csv", xml);
    }
}

/// <summary>CST-208: charts from the block's data, deterministic and accessible, with mistakes told in words.</summary>
public sealed class DataChartTests
{
    private static DataTable T() => DataReaders.Csv("x.csv", "mes;receita;despesa;categoria\nJan;1200;800;Mercado\nFev;1500;950;Transporte\nMar;900;1100;Lazer\n");

    [Theory]
    [InlineData("gráfico barras mes receita despesa título: Receita", "<rect", "Jan")]
    [InlineData("grafico linhas mes receita", "<polyline", "Fev")]
    [InlineData("gráfico pizza categoria despesa", "<path d='M", "Mercado")]
    [InlineData("gráfico dispersão receita despesa", "<circle", "1200")]
    public void Chart_IsValidSvg_WithItsMarksAndAnAccessibleLabel(string step, string mark, string label)
    {
        var r = DataQuery.Run(T(), [step]);
        var svg = r.ChartSvg()!;
        System.Xml.Linq.XDocument.Parse(svg);
        Assert.Contains(mark, svg);
        Assert.Contains("aria-label=", svg);
        Assert.Contains(label, System.Xml.Linq.XElement.Parse(svg).Attribute("aria-label")!.Value);
        Assert.Equal(svg, DataQuery.Run(T(), [step]).ChartSvg()); // deterministic
    }

    [Fact]
    public void Chart_MistakesAreWords_AndItIsTheLastStep()
    {
        Assert.Contains("não é numérica", Assert.Throws<InvalidDataException>(() => DataQuery.Run(T(), ["gráfico barras mes categoria"])).Message);
        Assert.Contains("Não há coluna", Assert.Throws<InvalidDataException>(() => DataQuery.Run(T(), ["gráfico barras mes lucro"])).Message);
        Assert.Contains("último passo", Assert.Throws<InvalidDataException>(() => DataQuery.Run(T(), ["gráfico barras mes receita", "limitar 2"])).Message);
        Assert.Contains("Gráfico desconhecido", Assert.Throws<InvalidDataException>(() => DataQuery.Run(T(), ["gráfico radar mes receita"])).Message);
        var html = DataBlock.RenderHtml("<pre data-lang=\"dados\"><code>fonte: x.csv\ngráfico barras mes receita</code></pre>", p => p == "x.csv" ? "mes;receita\nJan;1\n" : null);
        Assert.Contains("<figure class=\"urbe-plot urbe-chart\"><svg", html);
    }
}
