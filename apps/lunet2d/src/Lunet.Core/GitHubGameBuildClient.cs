using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Lunet.Core;

/// <summary>An independent Android game APK built by GitHub Actions, with run details.</summary>
public sealed record GameApkResult(byte[] Apk, string RunUrl);

/// <summary>Opt-in cloud build using a private repository controlled by the GitHub account owner.</summary>
public sealed class GitHubGameBuildClient(HttpClient http)
{
    private const string Repository = "lunet-game-builds";
    private const string Workflow = ".github/workflows/lunet-build.yml";
    private static readonly Regex PackagePattern = new(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*){2,}$");
    private const int MaxProjectBytes = 30 * 1024 * 1024;
    private const int MaxDownloadBytes = 120 * 1024 * 1024;

    /// <summary>Reject invalid package identifiers before submitting code.</summary>
    public static void ValidatePackageId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 180 || !PackagePattern.IsMatch(id)
            || id.Equals("io.lunet.studio", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Package ID inválido; exemplo: com.meusjogos.aventura.", nameof(id));
    }

    /// <summary>Upload project, request a private build, poll and download the APK.</summary>
    public async Task<GameApkResult> BuildAsync(string token, string packageId, string title, Guid gameId,
        Stream project, Action<string>? progress = null, CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Configure token em Git → Conta Git.");
        ValidatePackageId(packageId);
        if (gameId == Guid.Empty) throw new ArgumentException("GameId inválido.", nameof(gameId));
        if (project is null || !project.CanRead) throw new ArgumentException("ZIP do jogo inválido.", nameof(project));
        title = new string((title ?? "").Where(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '_' or '-').Take(30).ToArray()).Trim();
        if (title.Length == 0) title = "LunetGame";
        progress?.Invoke("Autenticando…");
        using var user = (await Json(HttpMethod.Get, "/user", token, null, cancel))!;
        string owner = user.RootElement.GetProperty("login").GetString()!;
        if (!Regex.IsMatch(owner, @"^[A-Za-z0-9-]{1,39}$")) throw new IOException("Login do GitHub inválido.");
        string root = $"/repos/{owner}/{Repository}";

        progress?.Invoke("Preparando o repositório privado…");
        using (var current = await Json(HttpMethod.Get, root, token, null, cancel, true))
        {
            if (current is null)
            {
                using var created = (await Json(HttpMethod.Post, "/user/repos", token,
                    JsonSerializer.Serialize(new { name = Repository, @private = true, auto_init = true,
                        description = "Build privado de jogos Lunet" }), cancel))!;
                if (!created.RootElement.GetProperty("private").GetBoolean())
                    throw new IOException("Não foi possível criar repositório privado.");
            }
            else if (!current.RootElement.GetProperty("private").GetBoolean())
                throw new IOException("O repositório lunet-game-builds existe mas é público. Torne-o privado.");
        }

        progress?.Invoke("Configurando workflow de build…");
        string workflowPath = root + "/contents/" + Workflow;
        string? existingWorkflow = await GetSha(workflowPath, token, cancel);
        if (existingWorkflow is null)
            await Put(workflowPath, token, GitHubGameBuildWorkflow.Source, "lunet: configurar build Android", null, cancel);

        progress?.Invoke("Enviando código e assets ao GitHub privado…");
        using var sourceZip = new MemoryStream();
        await project.CopyToAsync(sourceZip, cancel);
        if (sourceZip.Length is < 1 or > MaxProjectBytes)
            throw new IOException("ZIP precisa ter até 30 MiB neste protótipo de build na nuvem.");
        string zipPath = $"projects/{gameId:N}/source.zip";
        string path = root + "/contents/" + zipPath;
        await Put(path, token, Convert.ToBase64String(sourceZip.ToArray()), $"lunet: snapshot {title}",
            await GetSha(path, token, cancel), cancel, true);

        string buildId = Guid.NewGuid().ToString("N");
        progress?.Invoke("Iniciando compilação Android…");
        using (var response = await Send(HttpMethod.Post, root + "/actions/workflows/lunet-build.yml/dispatches",
            token, JsonSerializer.Serialize(new { @ref = "main", inputs = new
                { build_id = buildId, game_path = zipPath, package_id = packageId, game_title = title } }), cancel))
            await Ensure(response);

        string runsPath = root + "/actions/workflows/lunet-build.yml/runs?event=workflow_dispatch&per_page=30";
        for (int attempts = 0; attempts < 180; attempts++)
        {
            await Task.Delay(TimeSpan.FromSeconds(6), cancel);
            using var runs = (await Json(HttpMethod.Get, runsPath, token, null, cancel))!;
            foreach (var run in runs.RootElement.GetProperty("workflow_runs").EnumerateArray())
            {
                if (run.GetProperty("display_title").GetString() != "Lunet " + buildId) continue;
                string url = run.GetProperty("html_url").GetString()!;
                if (run.GetProperty("status").GetString() != "completed")
                {
                    progress?.Invoke("Compilando o jogo no GitHub Actions…");
                    break;
                }
                if (run.GetProperty("conclusion").GetString() != "success")
                    throw new IOException("Build falhou. Consulte os logs: " + url);
                progress?.Invoke("Baixando APK…");
                long runId = run.GetProperty("id").GetInt64();
                using var arts = (await Json(HttpMethod.Get, root + $"/actions/runs/{runId}/artifacts", token, null, cancel))!;
                foreach (var artifact in arts.RootElement.GetProperty("artifacts").EnumerateArray())
                {
                    if (artifact.GetProperty("name").GetString() != "lunet-game-apk") continue;
                    long artifactId = artifact.GetProperty("id").GetInt64();
                    byte[] archiveBytes = await Download(root + $"/actions/artifacts/{artifactId}/zip", token, cancel);
                    using var zip = new ZipArchive(new MemoryStream(archiveBytes, false), ZipArchiveMode.Read);
                    var apkFile = zip.Entries.FirstOrDefault(x => x.Name == "game.apk")
                        ?? throw new IOException("O build não devolveu game.apk.");
                    if (apkFile.Length is < 1 or > MaxDownloadBytes)
                        throw new IOException("Arquivo APK inválido.");
                    using var apk = new MemoryStream();
                    using (var input = apkFile.Open()) await input.CopyToAsync(apk, cancel);
                    return new GameApkResult(apk.ToArray(), url);
                }
                throw new IOException("O build terminou mas não publicou o APK: " + url);
            }
        }
        throw new TimeoutException("Build ainda em execução: acesse GitHub → lunet-game-builds → Actions.");
    }

    private async Task<string?> GetSha(string path, string token, CancellationToken ct)
    {
        using var result = await Json(HttpMethod.Get, path, token, null, ct, true);
        return result?.RootElement.GetProperty("sha").GetString();
    }

    private async Task Put(string path, string token, string content, string message, string? sha,
        CancellationToken ct, bool base64 = false)
    {
        var body = new Dictionary<string, string> {
            ["message"] = message, ["content"] = base64 ? content : Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            ["branch"] = "main"
        };
        if (sha is not null) body["sha"] = sha;
        using var result = await Json(HttpMethod.Put, path, token, JsonSerializer.Serialize(body), ct);
    }

    private async Task<JsonDocument?> Json(HttpMethod method, string path, string token, string? content,
        CancellationToken ct, bool allow404 = false)
    {
        using var response = await Send(method, path, token, content, ct);
        if (allow404 && response.StatusCode == HttpStatusCode.NotFound) return null;
        await Ensure(response);
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, string? body,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "https://api.github.com" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("Lunet-GameBuilder/1");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private static async Task Ensure(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var description = await response.Content.ReadAsStringAsync();
        string message;
        try
        {
            using var error = JsonDocument.Parse(description);
            message = error.RootElement.GetProperty("message").GetString() ?? "Erro do GitHub";
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { message = "Erro de resposta do GitHub"; }
        throw new IOException($"GitHub HTTP {(int)response.StatusCode}: {message}");
    }

    private async Task<byte[]> Download(string path, string token, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, path, token, null, ct);
        await Ensure(response);
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        byte[] buffer = new byte[65536];
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) != 0)
        {
            if (output.Length + count > MaxDownloadBytes) throw new IOException("Artifact muito grande.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
