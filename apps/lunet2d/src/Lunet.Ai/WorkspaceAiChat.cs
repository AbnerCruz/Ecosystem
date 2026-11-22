using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Lunet.Ai;

/// <summary>Conversa global sem vínculo com projetos ou ferramentas de escrita.
/// Históricos dos projetos permanecem gerenciados por LunetAiSession.</summary>
public sealed record WorkspaceAiTurn(string Role, string Text);

public sealed class WorkspaceAiChat
{
    private readonly string _path;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<WorkspaceAiTurn> _history;

    public WorkspaceAiChat(string path, HttpClient http)
    {
        _path = Path.GetFullPath(path);
        _http = http;
        if (!File.Exists(_path)) { _history = []; return; }
        try
        {
            if (new FileInfo(_path).Length > 256_000)
                throw new InvalidDataException("Conversa global grande demais. O original foi preservado.");
            var history = JsonSerializer.Deserialize<List<WorkspaceAiTurn>>(File.ReadAllText(_path)) ??
                throw new InvalidDataException("Histórico global inválido.");
            if (history.Count > 80 || history.Any(t =>
                t is null || t.Role is not ("user" or "assistant") ||
                string.IsNullOrWhiteSpace(t.Text) || t.Text.Length > 32_000))
                throw new InvalidDataException("Histórico global inválido. O original foi preservado.");
            _history = history;
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("Histórico global inválido. O original foi preservado.", e);
        }
    }

    public IReadOnlyList<WorkspaceAiTurn> History => _history.ToArray();

    public async Task<string> AskAsync(string prompt, LunetAiLoadedSettings ai, CancellationToken cancellationToken = default)
    {
        if (!ai.IsConfigured) throw new InvalidOperationException("Configure a IA na área de trabalho.");
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 16_000)
            throw new ArgumentException("Mensagem vazia ou grande demais.", nameof(prompt));
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Já existe uma mensagem em processamento.");
        try
        {
            var uri = LunetAiEndpoint.Validate(ai.Settings.Endpoint);
            var messages = new List<object>
            {
                new { role = "system", content = "Você é a IA do Lunet, ambiente de desenvolvimento de jogos 2D em C#. " +
                    "Neste chat GLOBAL não existe projeto aberto: não alegue acesso a arquivos, editor ou ferramentas de projeto. " +
                    "Ajude em dúvidas, planejamento, ideias, programação e uso do Lunet. Responda em português. " +
                    "Nunca finja executar, compilar ou alterar arquivos." }
            };
            foreach (var turn in _history.TakeLast(16))
                messages.Add(new { role = turn.Role, content = turn.Text });
            messages.Add(new { role = "user", content = prompt.Trim() });
            var body = JsonSerializer.Serialize(new { model = ai.Settings.Model, messages, max_tokens = 2048 });
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ai.ApiKey);
            if (ai.Settings.ProviderId == "anthropic")
            {
                request.Headers.TryAddWithoutValidation("x-api-key", ai.ApiKey);
                request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
            }
            using var response = await _http.SendAsync(request, cancellationToken);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var bounded = new MemoryStream();
            var chunk = new byte[4096];
            while (true)
            {
                int remaining = 512_000 - (int)bounded.Length;
                int read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining + 1)), cancellationToken);
                if (read == 0) break;
                if (read > remaining) throw new InvalidDataException("Resposta excessivamente grande.");
                bounded.Write(chunk, 0, read);
            }
            var json = Encoding.UTF8.GetString(bounded.ToArray());
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("O provedor retornou HTTP " + (int)response.StatusCode + ".");
            using var result = JsonDocument.Parse(json);
            var choices = result.RootElement.GetProperty("choices");
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                throw new InvalidDataException("Resposta do provedor sem alternativa de mensagem.");
            var text = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("O provedor não retornou texto.");
            var next = _history.Append(new WorkspaceAiTurn("user", prompt.Trim()))
                .Append(new WorkspaceAiTurn("assistant", text)).TakeLast(40).ToList();
            Save(next);
            _history.Clear();
            _history.AddRange(next);
            return text;
        }
        finally { _gate.Release(); }
    }

    public void Clear()
    {
        Save([]);
        _history.Clear();
    }

    private void Save(IReadOnlyList<WorkspaceAiTurn> next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(next));
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
        }
    }
}
