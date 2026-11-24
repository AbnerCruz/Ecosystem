using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Lunet.Ai;

/// <summary>Global conversation without project tools. The nullable metadata keeps legacy history readable.</summary>
public sealed record WorkspaceAiTurn(string Role, string Text, string? Id = null, string? RunId = null, bool Pending = false,
    bool Interrupted = false);

public sealed class WorkspaceAiChat
{
    private readonly string _path;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _historyGate = new();
    private readonly List<WorkspaceAiTurn> _history;
    private CancellationTokenSource? _active;
    public event Action? Changed;

    public WorkspaceAiChat(string path, HttpClient http)
    {
        _path = Path.GetFullPath(path);
        _http = http;
        if (!File.Exists(_path)) { _history = []; return; }
        try
        {
            if (new FileInfo(_path).Length > 4_000_000)
                throw new InvalidDataException("Conversa global grande demais. O original foi preservado.");
            var history = JsonSerializer.Deserialize<List<WorkspaceAiTurn>>(File.ReadAllText(_path)) ??
                throw new InvalidDataException("Histórico global inválido.");
            if (history.Any(t => t is null || t.Role is not ("user" or "assistant") || t.Text is null))
                throw new InvalidDataException("Histórico global inválido. O original foi preservado.");
            _history = history.Select((turn, index) => turn with
            {
                Id = turn.Id ?? "legacy:" + index, Pending = false, Interrupted = turn.Pending || turn.Interrupted
            }).ToList();
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("Histórico global inválido. O original foi preservado.", e);
        }
    }

    public IReadOnlyList<WorkspaceAiTurn> History { get { lock (_historyGate) return _history.ToArray(); } }
    public bool IsRunning { get { lock (_historyGate) return _active is not null; } }

    public void Cancel()
    {
        lock (_historyGate) _active?.Cancel();
    }

    public async Task<string> AskAsync(string prompt, LunetAiLoadedSettings ai, CancellationToken cancellationToken = default)
    {
        if (!ai.IsConfigured) throw new InvalidOperationException("Configure a IA na área de trabalho.");
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 16_000)
            throw new ArgumentException("Mensagem vazia ou grande demais.", nameof(prompt));
        var uri = LunetAiEndpoint.Validate(ai.Settings.Endpoint);
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Já existe uma mensagem em processamento.");

        var runId = Guid.NewGuid().ToString("N");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(_http.Timeout);
        var ct = source.Token;
        var text = new StringBuilder();
        string SafeText() => text.ToString().Replace(ai.ApiKey!, "•••", StringComparison.Ordinal);
        int index = -1;
        try
        {
            var messages = new List<object>
            {
                new { role = "system", content = "Você é a IA do Lunet, ambiente de criação de jogos 2D em C#. " +
                    "No chat GLOBAL, ajude com conceitos, planejamento e programação em português. " +
                    "Não existem projeto, arquivos ou ferramentas de execução nesta conversa." }
            };
            lock (_historyGate)
            {
                var interrupted = _history.Where(t => t.Interrupted && t.RunId is not null)
                    .Select(t => t.RunId!).ToHashSet(StringComparer.Ordinal);
                foreach (var turn in _history.Where(t => !t.Interrupted && !string.IsNullOrWhiteSpace(t.Text) &&
                    (t.RunId is null || !interrupted.Contains(t.RunId))).TakeLast(16))
                    messages.Add(new { role = turn.Role, content = turn.Text });
                _active = source;
                _history.Add(new WorkspaceAiTurn("user", prompt.Trim(), runId + ":user", runId));
                _history.Add(new WorkspaceAiTurn("assistant", "", runId + ":assistant", runId, Pending: true));
                index = _history.Count - 1;
                Save();
            }
            Signal();
            messages.Add(new { role = "user", content = prompt.Trim() });
            var body = JsonSerializer.Serialize(new { model = ai.Settings.Model, messages, max_tokens = 2048, stream = true });
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ai.ApiKey);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(LunetAiModelCatalog.Explain((int)response.StatusCode, ""));
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            string json;
            if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            {
                long nextUi = 0, nextSave = 0;
                json = await LunetAiStream.ReadAsync(stream, delta =>
                {
                    text.Append(delta);
                    long now = Environment.TickCount64;
                    if (now < nextUi) return;
                    lock (_historyGate)
                    {
                        _history[index] = _history[index] with { Text = SafeText() };
                        if (now >= nextSave) { Save(); nextSave = now + 750; }
                    }
                    Signal();
                    nextUi = now + 100;
                }, ct).ConfigureAwait(false);
            }
            else
            {
                using var bounded = new MemoryStream();
                var chunk = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
                {
                    if (bounded.Length + read > 512_000) throw new InvalidDataException("Resposta excessivamente grande.");
                    bounded.Write(chunk, 0, read);
                }
                json = Encoding.UTF8.GetString(bounded.ToArray());
            }

            using var result = JsonDocument.Parse(json);
            if (!result.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0 || !choices[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(content.GetString()))
                throw new InvalidDataException("O provedor não retornou texto.");
            if (choices[0].TryGetProperty("finish_reason", out var finish) && finish.ValueKind == JsonValueKind.String &&
                finish.GetString() != "stop")
                throw new InvalidDataException("A resposta não foi concluída pelo provedor.");

            var answer = content.GetString()!.Replace(ai.ApiKey!, "•••", StringComparison.Ordinal);
            lock (_historyGate)
            {
                _history[index] = _history[index] with { Text = answer, Pending = false };
                Save();
            }
            return answer;
        }
        catch
        {
            lock (_historyGate)
            {
                if (index >= 0)
                {
                    _history[index] = _history[index] with { Text = SafeText(), Pending = false, Interrupted = true };
                    Save();
                }
            }
            throw;
        }
        finally
        {
            lock (_historyGate) _active = null;
            _gate.Release();
            Signal();
        }
    }

    public void Clear()
    {
        lock (_historyGate)
        {
            if (_active is not null) throw new InvalidOperationException("Conclua a execução antes de limpar a conversa.");
            _history.Clear();
            Save();
        }
        Signal();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(_history));
            File.Move(temporary, _path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private void Signal()
    {
        try { Changed?.Invoke(); }
        catch (Exception) { /* A conversation can outlive its current view. */ }
    }
}
