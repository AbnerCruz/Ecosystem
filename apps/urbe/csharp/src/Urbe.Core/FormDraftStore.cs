using System.Text.Json;
using Urbe.Core.Assistant;
namespace Urbe.Core;
public sealed class FormDraftStore(IAssistantStorage storage)
{
    public static string Key(string vault, string path, string title) => "form-template-" + FormSession.Hash(vault + "\0" + path + "\0" + title) + ".json";
    public FormSession LoadOrCreate(string key, FormDefinition definition, string source)
    {
        var text = storage.Read(key);
        if (text is not null)
        {
            var saved = JsonSerializer.Deserialize<FormSession>(text, FormDefinition.Json) ?? throw new IOException("Rascunho ilegível; preservado.");
            if (saved.Definition.Version != 1) throw new IOException("Rascunho de uma versão mais nova; preservado.");
            if (saved.SourceHash != FormSession.Hash(source)) throw new IOException("O modelo mudou desde o rascunho. Preserve ou remova o rascunho antes de usar este modelo.");
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
