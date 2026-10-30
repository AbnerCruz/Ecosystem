using System.Text.Json.Nodes;
using System.Text.Json;
using Urbe.Core.Assistant;
namespace Urbe.Core;
public sealed class FormDraftStore(IAssistantStorage storage)
{
    public static string Key(string vault, string path, string title) => "form-template-" + FormSession.Hash(vault + "\0" + path + "\0" + title) + ".json";
    public FormSession LoadOrCreate(string key, FormDefinition definition, string source) => LoadOrCreate(key, definition, source, out _);

    /// <summary>
    /// The draft of this form, or a new one. When the template changed since the draft (CST-049), the form of the new
    /// template opens with the answers of the fields it still has (same id and type); <paramref name="left"/> are the
    /// answers that no longer fit, with their labels, for the person to see. It used to refuse ("preserve ou remova o
    /// rascunho"), with no place to do either: that note could never be made from that template again.
    /// </summary>
    public FormSession LoadOrCreate(string key, FormDefinition definition, string source, out IReadOnlyList<(string Label, string Value)> left)
    {
        left = [];
        var text = storage.Read(key);
        if (text is not null)
        {
            var saved = JsonSerializer.Deserialize<FormSession>(text, FormDefinition.Json) ?? throw new IOException("Rascunho ilegível; preservado.");
            if (saved.Definition.Version != 1) throw new IOException("Rascunho de uma versão mais nova; preservado.");
            if (saved.SourceHash != FormSession.Hash(source))
            {
                var carried = FormSession.Create(definition, source: source);
                var fields = definition.Fields.ToDictionary(f => f.Id, StringComparer.Ordinal);
                var dropped = new List<(string, string)>();
                foreach (var old in saved.Definition.Fields)
                {
                    var value = saved.Values[old.Id];
                    if (value is null || (value is JsonValue v && v.TryGetValue<string>(out var s) && s.Length == 0)) continue;
                    if (fields.TryGetValue(old.Id, out var now) && now.Type == old.Type) carried.Values[old.Id] = value.DeepClone();
                    else dropped.Add((old.Label.Length > 0 ? old.Label : old.Id, value is JsonValue one && one.TryGetValue<string>(out var t) ? t : value.ToJsonString()));
                }
                left = dropped;
                Save(key, carried);
                return carried;
            }
            saved.Definition.Check();
            return saved;
        }
        var session = FormSession.Create(definition, source: source);
        Save(key, session);
        return session;
    }
    public void Save(string key, FormSession session)
    {
        var text = JsonSerializer.Serialize(session, FormDefinition.Json);
        storage.Write(key, text);
        if (storage.Read(key) != text) throw new IOException("Não foi possível salvar as respostas do formulário.");
    }
    public void Delete(string key)
    {
        storage.Delete(key);
        if (storage.Read(key) is not null) throw new IOException("Não foi possível remover o rascunho do formulário.");
    }
}
