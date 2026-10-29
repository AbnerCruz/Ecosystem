using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;
namespace Urbe.Core.Tests;
public sealed class FormsTests
{
    [Fact]
    public void TypedValidationConditionsAndLiteralTemplateValues()
    {
        var definition = FormDefinition.Parse((JsonObject)JsonNode.Parse("""
        {"title":"Ficha","fields":[
          {"id":"name","label":"Nome","type":"text","required":true},
          {"id":"age","label":"Idade","type":"integer","min":0},
          {"id":"class","label":"Classe","type":"select","options":["Mago","Soldado"]},
          {"id":"spell","label":"Magia","type":"text","visible_when":"class","equals_value":"Mago","required":true},
          {"id":"day","label":"Data","type":"date"},
          {"id":"total","label":"Soma","type":"calculated","sum":["age"]}]}
        """)!);
        var session = FormSession.Create(definition);
        session.Values["age"] = 1.2;
        session.Values["class"] = "Soldado";
        session.Values["day"] = "2026-02-30";
        Assert.Equal(3, session.Validate().Count);
        session.Values["name"] = "A {{data}} $1";
        session.Values["age"] = 18;
        session.Values["day"] = "2026-10-25";
        Assert.Empty(session.Validate());
        Assert.Null(session.Answers()["spell"]);
        Assert.Equal(18, (double)session.Answers()["total"]!);
        session.Values["class"] = "Mago";
        Assert.Contains("spell", session.Validate().Keys);
        Assert.Equal("A {{data}} $1", NoteTemplateEngine.Fill("{{nome}}", "Ficha", DateTime.Now, new Dictionary<string, string> { ["nome"] = (string)session.Values["name"]! }));
    }
    [Theory]
    [InlineData("12a", false, "12")]
    [InlineData("abc", false, "")]
    [InlineData("-3,5x", false, "-3,5")]
    [InlineData("1.2.3", false, "1.23")]
    [InlineData("--4", false, "-4")]
    [InlineData("4-2", false, "42")]
    [InlineData("3,5", true, "35")]
    [InlineData("-7", true, "-7")]
    public void NumericFieldsOnlyTakeNumbers(string typed, bool integer, string kept) =>
        // CST-047: letters never get into a number field, whatever the keyboard sends
        Assert.Equal(kept, FormInput.NumberText(typed, integer));

    [Theory]
    [InlineData("3,5", 3.5)]
    [InlineData("-2", -2.0)]
    [InlineData("1.25", 1.25)]
    [InlineData("-", null)]
    [InlineData("", null)]
    public void NumberTextParsesWithCommaOrPoint(string text, double? number) =>
        Assert.Equal(number, FormInput.ParseNumber(text));

    [Fact]
    public void ErrorsSayWhatIsWrongWithTheValue()
    {
        var age = new FormField { Id = "age", Label = "Idade", Type = "integer", Min = 0, Max = 120 };
        var height = new FormField { Id = "h", Label = "Altura", Type = "number", Min = 0.5 };
        var session = FormSession.Create(new FormDefinition { Fields = [age, height] });
        session.Values["age"] = 1.5;
        session.Values["h"] = 0.2;
        Assert.Equal("Use um número inteiro.", session.ErrorFor(age));
        Assert.Equal("Use um valor a partir de 0,5.", session.ErrorFor(height));
        session.Values["age"] = 130;
        Assert.Equal("Use um valor entre 0 e 120.", session.ErrorFor(age));
        session.Values["age"] = "doze";
        Assert.Equal("Use um número.", session.ErrorFor(age));
        session.Values["age"] = 30;
        Assert.Null(session.ErrorFor(age));
    }

    [Fact]
    public void MultipleChoiceKeepsEverySelectedOption()
    {
        var tags = new FormField { Id = "t", Label = "Temas", Type = "multi_select", Options = ["Mapa", "Clima", "Povos"] };
        var session = FormSession.Create(new FormDefinition { Fields = [tags] });
        FormInput.Toggle(session, tags, "Mapa", true);
        FormInput.Toggle(session, tags, "Povos", true);
        FormInput.Toggle(session, tags, "Mapa", true);   // already on: not doubled
        Assert.Equal(["Mapa", "Povos"], ((JsonArray)session.Values["t"]!).Select(x => (string)x!));
        FormInput.Toggle(session, tags, "Mapa", false);
        Assert.Equal(["Povos"], ((JsonArray)session.Values["t"]!).Select(x => (string)x!));
        Assert.Empty(session.Validate());
    }

    [Fact]
    public void ConversationRoundTripKeepsDraftsProvenanceAndQueueIsolated()
    {
        var storage = new MemoryAssistantStorage();
        var store = new AssistantStore(storage);
        var definition = new FormDefinition { Title = "Nome", SourcePath = "Ficha.md", Fields = [new() { Id = "nome", Label = "Nome" }] };
        var conversation = new AiConversation { Id = "one", AgentId = "writer", Vault = "v" };
        var form = FormSession.Create(definition, "one", "writer", "# Antes");
        form.Values["nome"] = "Arven";
        conversation.Forms.Add(form);
        conversation.QueuedMessages.Add(new() { Id = "next", Role = "user", Content = [AiChatBlock.TextBlock("segunda mensagem")] });
        store.SaveConversationVerified(conversation);
        var loaded = store.GetConversation("one")!;
        var draft = Assert.Single(loaded.Forms);
        Assert.Equal("Arven", (string?)draft.Values["nome"]);
        Assert.Equal("one", draft.ConversationId);
        Assert.Equal("writer", draft.AgentId);
        Assert.Equal(form.SourceHash, draft.SourceHash);
        Assert.NotEqual(draft.SourceHash, FormSession.Hash("# Depois"));
        Assert.Single(loaded.QueuedMessages);
        Assert.Empty(new AiConversation().Forms);
        Assert.Empty(AssistantStore.ConversationFromJson(new JsonObject { ["id"] = "old" }).Forms);
    }
    [Fact]
    public void RejectDuplicateFieldsUnsupportedTypesCyclesAndInvalidMetadata()
    {
        Assert.Throws<ArgumentException>(() => FormDefinition.Parse((JsonObject)JsonNode.Parse("""{"title":"X","fields":[{"id":"x","label":"X","type":"script"}]}""")!));
        Assert.Throws<ArgumentException>(() => FormDefinition.Parse((JsonObject)JsonNode.Parse("""{"title":"X","fields":[{"id":"x","label":"X"},{"id":"x","label":"Y"}]}""")!));
        Assert.Throws<ArgumentException>(() => FormDefinition.Parse((JsonObject)JsonNode.Parse("""{"title":"X","fields":[{"id":"x","label":"X","visible_when":"y"}]}""")!));
        var form = FormDefinition.FromTemplate("Ficha", "{{titulo}} {{data}} {{nome}} {{nome}} {{idade}}");
        Assert.Equal(["nome", "idade"], form.Fields.Select(f => f.Id));
        var typed = FormDefinition.FromTemplate("Ficha", "<!-- urbe-form {\"title\":\"Ficha\",\"fields\":[{\"id\":\"idade\",\"label\":\"Idade\",\"type\":\"integer\"}]} -->\n{{idade}}");
        Assert.Equal("integer", Assert.Single(typed.Fields).Type);
    }
    private sealed class BrokenStorage : IAssistantStorage
    {
        public string? Read(string name) => null;
        public void Write(string name, string text) { }
        public void Delete(string name) { }
        public IEnumerable<string> List() => [];
    }
    [Fact]
    public void SilentPersistenceFailureIsNeverReportedAsSaved() =>
        Assert.Throws<IOException>(() => new AssistantStore(new BrokenStorage()).SaveConversationVerified(new() { Id = "one" }));
    [Fact]
    public void TemplateDraftResumesAndRefusesChangedSource()
    {
        var storage = new MemoryAssistantStorage();
        var drafts = new FormDraftStore(storage);
        var definition = FormDefinition.FromTemplate("Ficha", "{{nome}}");
        var key = FormDraftStore.Key("vault", "Modelos/Ficha.md", "Arven");
        var session = drafts.LoadOrCreate(key, definition, "{{nome}}");
        session.Values["nome"] = "Arven";
        drafts.Save(key, session);
        Assert.Equal("Arven", (string?)new FormDraftStore(storage).LoadOrCreate(key, definition, "{{nome}}").Values["nome"]);
        Assert.Throws<IOException>(() => drafts.LoadOrCreate(key, definition, "mudou {{nome}}"));
        Assert.Contains("Arven", storage.Read(key));
        drafts.Delete(key);
        Assert.Null(storage.Read(key));
    }
    [Fact]
    public void RepeatGroupsValidateChildrenAndExplicitDateOverridesTemplateDefault()
    {
        var form = new FormDefinition { Fields = [new() { Id = "people", Label = "Pessoas", Type = "repeat", Required = true,
            Fields = [new() { Id = "name", Label = "Nome", Required = true }] }] };
        var session = FormSession.Create(form);
        session.Values["people"] = new JsonArray(new JsonObject { ["name"] = "" });
        Assert.Contains("people", session.Validate().Keys);
        session.Values["people"] = new JsonArray(new JsonObject { ["name"] = "Arven" });
        Assert.Empty(session.Validate());
        var template = "<!-- urbe-form {} -->\n{{data}} {{nome}}";
        var output = NoteTemplateEngine.Fill(template, "X", new DateTime(2026, 10, 25), new Dictionary<string, string> { ["data"] = "2000-01-01", ["nome"] = "{{data}}" });
        Assert.Equal("2000-01-01 {{data}}", output);
    }

}
