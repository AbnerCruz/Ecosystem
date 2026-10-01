using System.Text.Json;

namespace Lunet.Core;

/// <summary>Identidade e credencial usadas nos commits e no acesso ao servidor Git (por exemplo GitHub).</summary>
public sealed class GitAccount
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";

    /// <summary>Usuário do servidor (no GitHub pode ficar vazio ao usar token).</summary>
    public string Username { get; set; } = "";

    /// <summary>Token de acesso pessoal. Fica no armazenamento privado do app.</summary>
    public string Token { get; set; } = "";

    public bool CanCommit => Name.Trim().Length > 0 && Email.Contains('@');
}

/// <summary>Guarda a <see cref="GitAccount"/> num arquivo do armazenamento privado do app.</summary>
public sealed class GitAccountStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public GitAccount Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<GitAccount>(File.ReadAllText(path), Options) ?? new GitAccount() : new GitAccount();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new GitAccount();
        }
    }

    public void Save(GitAccount account)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(account, Options));
    }
}
