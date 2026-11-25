using System.Text.Json;
using Lunet.Ai;

namespace Lunet.Desktop;

/// <summary>
/// The desktop key store for the AI (M7): one JSON file in the user's own app data, readable only by the user on
/// Linux/macOS. It is not an OS keychain; the AI setup says so, and the key never leaves this computer.
/// </summary>
public sealed class FileSecretVault(string path) : ISecretVault
{
    private Dictionary<string, string> Read()
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return []; }
    }

    private void Write(Dictionary<string, string> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(values));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(tmp, path, overwrite: true);
    }

    public string? Get(string name) => Read().GetValueOrDefault(name);
    public void Set(string name, string value) { var v = Read(); v[name] = value; Write(v); }
    public void Delete(string name) { var v = Read(); if (v.Remove(name)) Write(v); }
}
