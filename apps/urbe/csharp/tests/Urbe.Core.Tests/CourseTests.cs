using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-174: quizzes in notes and courses as products from live notes.</summary>
public sealed class CourseTests
{
    private const string Quiz = "? Qual a derivada de x²?\n- [ ] x\n- [x] 2x\n> Regra da potência.\n\n? Quais são primos?\n- [x] 2\n- [x] 3\n- [ ] 4\n\n? Sem certa\n- [ ] a\n- [ ] b\n";

    [Fact]
    public void Quiz_ParsesQuestions_AndReportsMistakes()
    {
        var problems = new List<string>();
        var q = QuizBlock.Parse(Quiz, problems);
        Assert.Equal(2, q.Count);
        Assert.False(q[0].Multiple);
        Assert.True(q[1].Multiple);
        Assert.Equal("Regra da potência.", q[0].Explanation);
        Assert.Contains(problems, p => p.Contains("não marca nenhuma alternativa certa"));
    }

    private static (PublicationSourceCatalog, PublicationProject) Course()
    {
        var store = new DocumentStore();
        var a = store.Upsert(new DocumentInput { Path = "Curso/Derivadas.md", Content = "---\nobjetivos: [entender a derivada, calcular derivadas simples]\n---\n# Derivadas\n\nBoas-vindas.\n\n## Conceito\n\nTaxa de variação.\n\n```python\n# não é título\n```\n\n## Exercícios\n\n```quiz\n" + Quiz + "```\n" });
        var b = store.Upsert(new DocumentInput { Path = "Curso/Integrais.md", Content = "# Integrais\n\n## Ideia\n\nÁrea.\n" });
        var project = PublicationProject.Create("Cálculo I", a.Id).AddSource(b.Id) with { Design = new PublicationDesign { Layout = "course" } };
        return (new PublicationSourceCatalog(store), project);
    }

    [Fact]
    public void Course_ModulesLessonsObjectivesAndInteractiveQuizzes()
    {
        var (catalog, project) = Course();
        using var _c = catalog;
        var modules = PublicationCourse.Modules(project, catalog, null, []);
        Assert.Equal(["Derivadas", "Integrais"], modules.Select(m => m.Title));
        Assert.Equal(["Introdução", "Conceito", "Exercícios"], modules[0].Lessons.Select(l => l.Title));
        Assert.Equal(["entender a derivada", "calcular derivadas simples"], modules[0].Objectives);
        Assert.Contains("# não é título", modules[0].Lessons[1].Html);
        var html = PublicationHtml.Export(project, catalog).Html;
        Assert.Contains("<nav class=\"toc\"", html);
        Assert.Contains("data-ok=\"1\"", html);
        Assert.Contains("Objetivos", html);
        Assert.Contains("localStorage", html);
        Assert.DoesNotContain("Gabarito", html); // interactive: answers come by checking
    }

    [Fact]
    public async Task StaticFormats_GetTheQuestionsAndTheAnswerKey()
    {
        var (catalog, project) = Course();
        using var _c = catalog;
        var article = project with { Design = new PublicationDesign { Layout = "article" } };
        Assert.Contains("Gabarito", PublicationHtml.Export(article, catalog).Html);
        var docx = await PublicationDocx.ExportAsync(article, catalog, cancellationToken: TestContext.Current.CancellationToken);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(docx.Bytes));
        using var r = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var xml = r.ReadToEnd();
        Assert.Contains("1. Qual a derivada de x²?", xml);
        Assert.Contains("1) b — Regra da potência.; 2) a, b", xml);
    }
}
