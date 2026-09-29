using System.Text.Json;

namespace Lunet.Storage;

/// <summary>Armazenamento de dados salvos do jogo, por chave.</summary>
public interface ISaveStore
{
    /// <summary>Diz se existe salvamento com a chave.</summary>
    /// <param name="key">Chave.</param>
    /// <returns>Verdadeiro se existe.</returns>
    bool Exists(string key);
    /// <summary>Lê o texto salvo.</summary>
    /// <param name="key">Chave.</param>
    /// <returns>O texto, ou nulo se não existe.</returns>
    string? ReadText(string key);
    /// <summary>Grava o texto.</summary>
    /// <param name="key">Chave.</param>
    /// <param name="text">Conteúdo.</param>
    void WriteText(string key, string text);
    /// <summary>Apaga o salvamento.</summary>
    /// <param name="key">Chave.</param>
    void Delete(string key);
}

/// <summary>Salvamento em arquivos de uma pasta. Gravação atômica (arquivo temporário + rename).</summary>
public sealed class DirectorySaveStore : ISaveStore
{
    private readonly string _root;

    /// <summary>Guarda os dados em arquivos de uma pasta.</summary>
    /// <param name="directory">Pasta dos salvamentos.</param>
    public DirectorySaveStore(string directory) => _root = Path.GetFullPath(directory);

    /// <summary>Diz se existe salvamento com a chave.</summary>
    /// <param name="key">Chave (letras, números, _ - .).</param>
    /// <returns>Verdadeiro se existe.</returns>
    public bool Exists(string key) => File.Exists(PathFor(key));

    /// <summary>Lê o texto salvo.</summary>
    /// <param name="key">Chave.</param>
    /// <returns>O texto, ou nulo se não existe.</returns>
    public string? ReadText(string key)
    {
        var path = PathFor(key);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>Grava o texto de forma atômica.</summary>
    /// <param name="key">Chave.</param>
    /// <param name="text">Conteúdo.</param>
    public void WriteText(string key, string text)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(_root);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Apaga o salvamento.</summary>
    /// <param name="key">Chave.</param>
    public void Delete(string key) => File.Delete(PathFor(key));

    private string PathFor(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 64 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.')) || key.StartsWith('.'))
            throw new ArgumentException("Chave inválida: use até 64 letras, números, '_', '-' ou '.'.", nameof(key));
        return Path.Combine(_root, key + ".sav");
    }
}

/// <summary>Salvamento em memória (não persiste). Usado quando o host não fornece armazenamento.</summary>
public sealed class MemorySaveStore : ISaveStore
{
    private readonly Dictionary<string, string> _data = new();
    /// <summary>Diz se existe salvamento com a chave.</summary>
    /// <param name="key">Chave.</param>
    /// <returns>Verdadeiro se existe.</returns>
    public bool Exists(string key) => _data.ContainsKey(key);
    /// <summary>Lê o texto salvo.</summary>
    /// <param name="key">Chave.</param>
    /// <returns>O texto, ou nulo se não existe.</returns>
    public string? ReadText(string key) => _data.GetValueOrDefault(key);
    /// <summary>Grava o texto.</summary>
    /// <param name="key">Chave.</param>
    /// <param name="text">Conteúdo.</param>
    public void WriteText(string key, string text) => _data[key] = text;
    /// <summary>Apaga o salvamento.</summary>
    /// <param name="key">Chave.</param>
    public void Delete(string key) => _data.Remove(key);
}

/// <summary>API de salvamento exposta ao jogo (<c>Save</c>): JSON tipado sobre um <see cref="ISaveStore"/>.</summary>
public sealed class SaveData
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = true };
    private readonly ISaveStore _store;

    /// <summary>Cria a API de salvamento sobre um armazenamento.</summary>
    /// <param name="store">Onde guardar os dados.</param>
    public SaveData(ISaveStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Diz se existe salvamento com a chave.</summary>
    /// <param name="key">Chave.</param>
    /// <returns>Verdadeiro se existe.</returns>
    public bool Exists(string key) => _store.Exists(key);

    /// <summary>Salva o valor como JSON.</summary>
    /// <param name="key">Chave.</param>
    /// <param name="value">Objeto a salvar (campos e propriedades públicos).</param>
    public void Save<T>(string key, T value) => _store.WriteText(key, JsonSerializer.Serialize(value, Options));

    /// <summary>Lê o valor salvo; se não existir ou estiver corrompido, devolve <paramref name="fallback"/>.</summary>
    public T Load<T>(string key, T fallback)
    {
        var text = _store.ReadText(key);
        if (text is null) return fallback;
        try { return JsonSerializer.Deserialize<T>(text, Options) ?? fallback; }
        catch (JsonException) { return fallback; }
    }

    /// <summary>Apaga o salvamento.</summary>
    /// <param name="key">Chave.</param>
    public void Delete(string key) => _store.Delete(key);
}
