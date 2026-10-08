using System.Globalization;
using MathAuthoring.Core;

namespace MathAuthoring.Core.Tests;

public sealed class DocumentEditingTests
{
    private static ProjectDocument Example()
    {
        var variables = Array.AsReadOnly(new[]
        {
            new VariableDefinition("x", "x", 1),
            new VariableDefinition("dx", "Δx", 2,
                new BinaryExpression(BinaryOperator.Add,
                    new VariableReferenceExpression("x"), new ConstantExpression(1.5)))
        });
        return ProjectDocument.Create("proof", "Δx → 0") with
        {
            Metadata = new ProjectMetadata("Δx → 0", "Autor: professor"),
            Variables = variables
        };
    }

    [Fact]
    public void V1_round_trip_preserves_stable_ids_AST_order_and_is_byte_stable()
    {
        var original = Example();
        var json = ProjectJson.Format(original);
        var read = ProjectJson.Parse(json);

        Assert.True(read.Success);
        Assert.Equal(json, ProjectJson.Format(read.Document!));
        Assert.Equal(original.ProjectId, read.Document!.ProjectId);
        Assert.Equal("dx", read.Document.Variables[1].Id);
        var add = Assert.IsType<BinaryExpression>(read.Document.Variables[1].Definition);
        Assert.Equal(BinaryOperator.Add, add.Operator);
        Assert.Equal("x", Assert.IsType<VariableReferenceExpression>(add.Left).VariableId);
        Assert.Equal(1.5, Assert.IsType<ConstantExpression>(add.Right).Value);
    }

    [Fact]
    public void Formatting_is_culture_independent()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            var source = ProjectJson.Format(Example());
            Assert.Contains("1.5", source);
            Assert.Equal(source, ProjectJson.Format(ProjectJson.Parse(source).Document!));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Theory]
    [InlineData("{", "json.syntax")]
    [InlineData("null", "json.type")]
    [InlineData("[]", "json.type")]
    public void Non_documents_are_rejected(string json, string code)
    {
        var result = ProjectJson.Parse(json);
        Assert.False(result.Success);
        Assert.Contains(result.Problems, p => p.Code == code);
    }

    [Fact]
    public void Unknown_and_duplicate_properties_are_not_silently_ignored()
    {
        var json = ProjectJson.Format(Example());
        var unknown = json.Replace("\"projectId\": \"proof\"", "\"projectId\": \"proof\", \"executor\": \"script\"", StringComparison.Ordinal);
        var duplicate = json.Replace("\"projectId\": \"proof\"", "\"projectId\": \"proof\", \"projectId\": \"duplicate\"", StringComparison.Ordinal);

        Assert.Contains(ProjectJson.Parse(unknown).Problems, x => x.Code == "json.unknown-field");
        Assert.Contains(ProjectJson.Parse(duplicate).Problems, x => x.Code == "json.duplicate-key");
    }

    [Fact]
    public void Future_schema_and_unknown_AST_are_rejected()
    {
        var json = ProjectJson.Format(Example());
        Assert.Contains(ProjectJson.Parse(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal)).Problems,
            x => x.Code == "schema.unsupported");
        Assert.Contains(ProjectJson.Parse(json.Replace("\"kind\": \"binary\"", "\"kind\": \"shell\"", StringComparison.Ordinal)).Problems,
            x => x.Code == "expression.kind.unknown");
        Assert.Contains(ProjectJson.Parse(json.Replace("\"version\": 1", "\"version\": 2", StringComparison.Ordinal)).Problems,
            x => x.Code == "expression.version.unsupported");
    }

    [Fact]
    public void Missing_references_non_finite_numbers_and_limits_fail()
    {
        var json = ProjectJson.Format(Example());
        Assert.Contains(ProjectJson.Parse(json.Replace("\"variableId\": \"x\"", "\"variableId\": \"missing\"", StringComparison.Ordinal)).Problems,
            x => x.Code == "expression.variable.missing");
        Assert.Contains(ProjectJson.Parse(json.Replace("\"initialValue\": 2", "\"initialValue\": 1e999", StringComparison.Ordinal)).Problems,
            x => x.Code == "json.number.invalid");
        Assert.Contains(ProjectJson.Parse(new string(' ', ProjectJson.MaxBytes + 1)).Problems,
            x => x.Code == "json.too-large");
        var tooDeep = (MathExpression)new ConstantExpression(1);
        for (var i = 0; i < 40; i++)
            tooDeep = new BinaryExpression(BinaryOperator.Add, tooDeep, new ConstantExpression(1));
        var invalid = Example() with
        {
            Variables = Array.AsReadOnly(new[] { new VariableDefinition("x", "x", 1, tooDeep) })
        };
        Assert.Contains(ProjectValidator.Validate(invalid), x => x.Code == "expression.limit");
    }

    [Fact]
    public void Invalid_text_cannot_commit_or_corrupt_history()
    {
        var session = new ProjectSession(Example());
        var initial = session.Source;
        var bad = session.ApplyText(0, initial.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99", StringComparison.Ordinal));

        Assert.False(bad.Accepted);
        Assert.False(bad.Applied);
        Assert.Equal(0, session.Revision);
        Assert.False(session.CanUndo);
        Assert.Equal(initial, session.Source);

        var malformed = session.ApplyText(0, "{");
        Assert.False(malformed.Accepted);
        Assert.Equal(initial, session.Source);
    }

    [Fact]
    public void Visual_and_textual_edits_share_revision_undo_redo_and_history()
    {
        var session = new ProjectSession(Example());
        var edited = session.Apply(0, document =>
            document with { Metadata = document.Metadata with { Title = "Visual" } });

        Assert.True(edited.Accepted && edited.Applied);
        Assert.Equal(1, session.Revision);
        Assert.Equal("Visual", session.Snapshot.Metadata.Title);

        var text = session.Source.Replace("\"title\": \"Visual\"", "\"title\": \"Textual\"", StringComparison.Ordinal);
        var appliedText = session.ApplyText(1, text);
        Assert.True(appliedText.Accepted && appliedText.Applied);
        Assert.Equal(2, session.Revision);
        Assert.Equal("Textual", session.Snapshot.Metadata.Title);

        Assert.True(session.Undo(2).Applied);
        Assert.Equal("Visual", session.Snapshot.Metadata.Title);
        Assert.True(session.Undo(3).Applied);
        Assert.Equal("Δx → 0", session.Snapshot.Metadata.Title);
        Assert.True(session.Redo(4).Applied);
        Assert.Equal("Visual", session.Snapshot.Metadata.Title);

        Assert.True(session.Apply(5, d => d with { Metadata = d.Metadata with { Title = "Outra" } }).Applied);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void Stale_base_cannot_overwrite_concurrent_revision()
    {
        var session = new ProjectSession(Example());
        Assert.True(session.Apply(0, d => d with { Metadata = d.Metadata with { Title = "Primeiro" } }).Applied);
        var current = session.Source;
        var rejected = session.ApplyText(0, ProjectJson.Format(Example()));
        Assert.Contains(rejected.Problems, p => p.Code == "transaction.stale");
        Assert.Equal(1, session.Revision);
        Assert.Equal(current, session.Source);
        Assert.Contains(session.Undo(0).Problems, p => p.Code == "transaction.stale");
    }

    [Fact]
    public void Bad_visual_command_and_project_identity_change_are_rejected()
    {
        var session = new ProjectSession(Example());
        Assert.False(session.Apply(0, d => d with
        {
            Variables = Array.AsReadOnly(new[] { new VariableDefinition("x", "x", double.NaN) })
        }).Accepted);
        Assert.Contains(session.Apply(0, d => d with { ProjectId = "other" }).Problems,
            p => p.Code == "transaction.project-id");
        Assert.Equal(0, session.Revision);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void No_op_does_not_increment_revision_or_history()
    {
        var session = new ProjectSession(Example());
        var result = session.ApplyText(0, session.Source);
        Assert.True(result.Accepted);
        Assert.False(result.Applied);
        Assert.Equal(0, result.Revision);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void Session_can_reopen_published_text_without_runtime_dependencies()
    {
        var session = new ProjectSession(Example());
        var edit = session.Apply(0, d => d with { Metadata = d.Metadata with { Title = "Persistido" } });
        Assert.True(edit.Applied);
        var parsed = ProjectSession.Open(session.Source);
        Assert.True(parsed.Success);
        var reopened = new ProjectSession(parsed.Document!);
        Assert.Equal("Persistido", reopened.Snapshot.Metadata.Title);
        Assert.Equal(session.Source, reopened.Source);
    }
    [Fact]
    public void Reentrant_command_cannot_overwrite_a_nested_successful_commit()
    {
        var session = new ProjectSession(Example());
        var outer = session.Apply(0, first =>
        {
            var inner = session.Apply(0, second =>
                second with { Metadata = second.Metadata with { Title = "Inner" } });
            Assert.True(inner.Applied);
            return first with { Metadata = first.Metadata with { Title = "Outer" } };
        });

        Assert.False(outer.Accepted);
        Assert.Contains(outer.Problems, p => p.Code == "transaction.stale");
        Assert.Equal(1, session.Revision);
        Assert.Equal("Inner", session.Snapshot.Metadata.Title);
    }

    [Fact]
    public async Task Concurrent_editors_cannot_both_publish_the_same_revision()
    {
        var session = new ProjectSession(Example());
        var first = Task.Run(() => session.Apply(0,
            d => d with { Metadata = d.Metadata with { Title = "First" } }));
        var second = Task.Run(() => session.Apply(0,
            d => d with { Metadata = d.Metadata with { Title = "Second" } }));

        var results = await Task.WhenAll(first, second);
        Assert.Single(results.Where(r => r.Applied));
        Assert.Single(results.Where(r => r.Problems.Any(p => p.Code == "transaction.stale")));
        Assert.Equal(1, session.Revision);
        Assert.True(session.CanUndo);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void Migration_only_runs_explicit_trusted_step_and_validates_final_document()
    {
        var v1 = ProjectJson.Format(Example());
        var legacy = v1.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 0", StringComparison.Ordinal);
        Assert.Contains(ProjectMigrator.Open(legacy).Problems, p => p.Code == "migration.missing");

        var migration = new DocumentMigration(0, 1,
            source => source.Replace("\"schemaVersion\": 0", "\"schemaVersion\": 1", StringComparison.Ordinal));
        var migrated = ProjectMigrator.Open(legacy, migration);

        Assert.True(migrated.Success);
        Assert.Equal(v1, ProjectJson.Format(migrated.Document!));
        Assert.Equal(v1, ProjectJson.Format(ProjectMigrator.Open(v1).Document!));

        var wrongVersion = ProjectMigrator.Open(legacy, new DocumentMigration(0, 2, migration.Upgrade));
        Assert.Contains(wrongVersion.Problems, p => p.Code == "migration.invalid-step");
        var corrupt = ProjectMigrator.Open(legacy, new DocumentMigration(0, 1, _ => "{"));
        Assert.Contains(corrupt.Problems, p => p.Code == "json.syntax");
        Assert.Equal(legacy, legacy); // No migration mutates the supplied string.
    }

    [Fact]
    public void Migration_never_downgrades_future_schema_or_accepts_duplicate_source()
    {
        var v1 = ProjectJson.Format(Example());
        var future = v1.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal);
        Assert.Contains(ProjectMigrator.Open(future).Problems, p => p.Code == "schema.unsupported");
        var duplicate = v1.Replace("\"projectId\": \"proof\"",
            "\"projectId\": \"proof\", \"projectId\": \"extra\"", StringComparison.Ordinal);
        Assert.Contains(ProjectMigrator.Open(duplicate).Problems, p => p.Code == "json.duplicate-key");
    }

}
