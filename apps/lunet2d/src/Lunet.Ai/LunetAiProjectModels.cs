using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lunet.Ai;

/// <summary>Optional model override for one game and one provider configuration.</summary>
public sealed class LunetAiProjectModels(string privateDirectory)
{
    public string? Get(string gameId, LunetAiSettings settings)
    {
        try
        {
            var path = PathFor(gameId, settings);
            return File.Exists(path) && new FileInfo(path).Length <= 2048
                ? JsonSerializer.Deserialize<string>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Set(string gameId, LunetAiSettings settings, string? model)
    {
        if (model is { Length: > 240 } || model?.Any(char.IsControl) == true)
            throw new ArgumentException("Identificador de modelo inválido.", nameof(model));
        var path = PathFor(gameId, settings);
        if (string.IsNullOrWhiteSpace(model)) { File.Delete(path); return; }
        Directory.CreateDirectory(privateDirectory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(model.Trim()));
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private string PathFor(string gameId, LunetAiSettings settings)
    {
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new[] { gameId, settings.ProviderId, settings.Endpoint }))));
        return Path.Combine(privateDirectory, key + ".json");
    }
}
