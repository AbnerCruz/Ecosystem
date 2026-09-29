using System.Text.Json;

namespace Lunet.Storage;

/// <summary>Armazenamento de dados salvos do jogo, por chave.</summary>
public interface ISaveStore
{
    bool Exists(string key);
    string? ReadText(string key);
    void WriteText(string key, string text);
    void Delete(string key);
}

/// <summary>Salvamento em arquivos de uma pasta. Gravação atômica (arquivo temporário + rename).</summary>
public sealed class DirectorySaveStore : ISaveStore
{
    private readonly string _root;

    public DirectorySaveStore(string directory) => _root = Path.GetFullPath(directory);

    public bool Exists(string key) => File.Exists(PathFor(key));

    public string? ReadText(string key)
    {
        var path = PathFor(key);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void WriteText(string key, string text)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(_root);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }

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
    public bool Exists(string key) => _data.ContainsKey(key);
    public string? ReadText(string key) => _data.GetValueOrDefault(key);
    public void WriteText(string key, string text) => _data[key] = text;
    public void Delete(string key) => _data.Remove(key);
}

/// <summary>API de salvamento exposta ao jogo (<c>Save</c>): JSON tipado sobre um <see cref="ISaveStore"/>.</summary>
public sealed class SaveData
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, IncludeFields = true };
    private readonly ISaveStore _store;

    public SaveData(ISaveStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    public bool Exists(string key) => _store.Exists(key);

    public void Save<T>(string key, T value) => _store.WriteText(key, JsonSerializer.Serialize(value, Options));

    /// <summary>Lê o valor salvo; se não existir ou estiver corrompido, devolve <paramref name="fallback"/>.</summary>
    public T Load<T>(string key, T fallback)
    {
        var text = _store.ReadText(key);
        if (text is null) return fallback;
        try { return JsonSerializer.Deserialize<T>(text, Options) ?? fallback; }
        catch (JsonException) { return fallback; }
    }

    public void Delete(string key) => _store.Delete(key);
}
