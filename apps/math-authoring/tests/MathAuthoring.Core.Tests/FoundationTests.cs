using MathAuthoring.Core;

namespace MathAuthoring.Core.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void Identity_is_stable_and_not_the_old_codename()
    {
        Assert.Equal("math-authoring", ProductIdentity.Id);
        Assert.NotEqual("math-studio", ProductIdentity.Id);
        Assert.Equal(1, ProductIdentity.CurrentSchemaVersion);
    }

    [Fact]
    public void Empty_project_is_headless_and_valid()
    {
        var project = ProjectDocument.Create("demo", "Δx → 0");

        Assert.Empty(ProjectValidator.Validate(project));
        Assert.Single(project.Scenes);
        Assert.Single(project.ExportProfiles);
        Assert.Equal(1280, project.ExportProfiles[0].Width);
        Assert.Equal(720, project.ExportProfiles[0].Height);
        Assert.Equal(30, project.ExportProfiles[0].FramesPerSecond);
    }

    [Fact]
    public void Validator_rejects_duplicate_ids_and_non_finite_values()
    {
        var variables = Array.AsReadOnly(new[]
        {
            new VariableDefinition("dx", "Δx", 2),
            new VariableDefinition("dx", "duplicada", double.NaN)
        });
        var project = new ProjectDocument(
            1,
            "demo",
            new ProjectMetadata("teste"),
            variables,
            Array.AsReadOnly(new[] { new SceneDocument("scene-1", "Cena", Array.AsReadOnly(Array.Empty<string>())) }),
            Array.AsReadOnly(new[] { new ExportProfile("p", "P", 1280, 720, 30) }));

        var codes = ProjectValidator.Validate(project).Select(x => x.Code).ToArray();

        Assert.Contains("variable.id.duplicate", codes);
        Assert.Contains("variable.value.non-finite", codes);
    }

    [Fact]
    public void Core_has_no_product_or_ui_dependencies()
    {
        var references = typeof(ProductIdentity).Assembly.GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToArray();

        Assert.All(references, name =>
            Assert.StartsWith("System", name, StringComparison.Ordinal));
    }

    [Fact]
    public void Expression_model_is_structured_not_a_string_authority()
    {
        MathExpression expression = new BinaryExpression(
            BinaryOperator.Power,
            new VariableReferenceExpression("x"),
            new ConstantExpression(2));

        var power = Assert.IsType<BinaryExpression>(expression);
        Assert.Equal(BinaryOperator.Power, power.Operator);
        Assert.IsType<VariableReferenceExpression>(power.Left);
        Assert.IsType<ConstantExpression>(power.Right);
    }
}
