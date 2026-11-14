using System.Text.Json;

namespace Lunet.Ai;

/// <summary>A provider the person can pick by name instead of typing an endpoint.</summary>
public sealed record LunetAiProvider(string Id, string Name, string Endpoint, string KeyHint, string KeyUrl,
    IReadOnlyList<string> SuggestedModels);

/// <summary>Known OpenAI-compatible chat-completions providers (LUNET-481). Endpoints are public documentation, not secrets.</summary>
public static class LunetAiProviders
{
    public static IReadOnlyList<LunetAiProvider> All { get; } =
    [
        new("openrouter", "OpenRouter", "https://openrouter.ai/api/v1/chat/completions", "sk-or-…",
            "https://openrouter.ai/keys",
            ["anthropic/claude-sonnet-5", "openai/gpt-5.1", "google/gemini-3-pro", "deepseek/deepseek-chat", "qwen/qwen3-coder"]),
        new("anthropic", "Anthropic", "https://api.anthropic.com/v1/chat/completions", "sk-ant-…",
            "https://console.anthropic.com/settings/keys",
            ["claude-sonnet-5", "claude-opus-5-5", "claude-haiku-4-5-20251001"]),
        new("openai", "OpenAI", "https://api.openai.com/v1/chat/completions", "sk-…",
            "https://platform.openai.com/api-keys", ["gpt-5.1", "gpt-5-mini"]),
        new("gemini", "Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "AIza…",
            "https://aistudio.google.com/apikey", ["gemini-3-pro", "gemini-2.5-flash"]),
        new("deepseek", "DeepSeek", "https://api.deepseek.com/v1/chat/completions", "sk-…",
            "https://platform.deepseek.com/api_keys", ["deepseek-chat", "deepseek-reasoner"]),
        new("groq", "Groq", "https://api.groq.com/openai/v1/chat/completions", "gsk_…",
            "https://console.groq.com/keys", ["llama-3.3-70b-versatile", "qwen/qwen3-32b"]),
        new("custom", "Outro (compatível com OpenAI)", "", "chave", "", []),
    ];

    public static LunetAiProvider Find(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal)) ?? All[^1];
}

/// <summary>Non-secret AI preferences. The API key never lives here.</summary>
public sealed record LunetAiSettings(string ProviderId, string Endpoint, string Model, int MaxSteps, bool AllowWrites)
{
    public static LunetAiSettings Default { get; } =
        new("openrouter", LunetAiProviders.All[0].Endpoint, "", LunetAiRunOptions.DefaultMaxSteps, true);
}

public sealed record LunetAiLoadedSettings(LunetAiSettings Settings, string? ApiKey)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Settings.Model)
                                && !string.IsNullOrWhiteSpace(Settings.Endpoint);
}

/// <summary>Platform secret storage (Android Keystore on device). Implementations must never write plaintext to disk.</summary>
public interface ISecretVault
{
    string? Get(string name);
    void Set(string name, string value);
    void Delete(string name);
}

/// <summary>Persists AI preferences atomically in app-private storage and the key in the <see cref="ISecretVault"/>.</summary>
public sealed class LunetAiSettingsStore(string path, ISecretVault vault)
{
    public const string SecretName = "lunet.ai.apiKey";

    public LunetAiLoadedSettings Load()
    {
        var settings = LunetAiSettings.Default;
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= 64_000)
                settings = JsonSerializer.Deserialize<LunetAiSettings>(File.ReadAllText(path)) ?? settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        settings = settings with
        {
            MaxSteps = IsValidSteps(settings.MaxSteps) ? settings.MaxSteps : LunetAiRunOptions.DefaultMaxSteps,
            ProviderId = string.IsNullOrWhiteSpace(settings.ProviderId) ? "custom" : settings.ProviderId,
            Endpoint = settings.Endpoint ?? "",
            Model = settings.Model ?? "",
        };
        string? key = null;
        try { key = vault.Get(SecretName); }
        catch (Exception) { key = null; } // a broken keystore entry means "not configured", never a crash
        return new LunetAiLoadedSettings(settings, key);
    }

    public void Save(LunetAiSettings settings, string? apiKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
        File.Move(temporary, path, overwrite: true);
        if (!string.IsNullOrWhiteSpace(apiKey)) vault.Set(SecretName, apiKey.Trim());
    }

    public void ClearKey() => vault.Delete(SecretName);

    private static bool IsValidSteps(int steps)
    {
        try { LunetAiRunOptions.ValidateMaxSteps(steps); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}
