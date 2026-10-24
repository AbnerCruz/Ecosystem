using System.Security.Cryptography;
using Urbe.Core.Assistant;

namespace Urbe.LocalAi;

/// <summary>
/// The models' files in a folder of the app's own data (never the vault): the download resumes where it stopped, is
/// written as a .part file and becomes the model only after its SHA-256 matches. Nothing here needs the network except
/// <see cref="InstallAsync"/>.
/// </summary>
public sealed class ModelStore(string folder, HttpClient? http = null)
{
    private readonly HttpClient _http = http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

    public string PathOf(LocalModel model) => Path.Combine(folder, model.File);

    /// <summary>The file is there with the right size. (The hash was checked when it was kept; checking 5 GB at every start would take a minute.)</summary>
    public bool IsInstalled(LocalModel model) => File.Exists(PathOf(model)) && new FileInfo(PathOf(model)).Length == model.Bytes;

    public void Remove(LocalModel model)
    {
        foreach (var path in new[] { PathOf(model), PathOf(model) + ".part" })
            if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Bytes of a download left half-way (for "continuar").</summary>
    public long PartialBytes(LocalModel model) => File.Exists(PathOf(model) + ".part") ? new FileInfo(PathOf(model) + ".part").Length : 0;

    public async Task InstallAsync(LocalModel model, IProgress<(long Done, long Total)>? progress, CancellationToken cancel)
    {
        if (IsInstalled(model)) return;
        Directory.CreateDirectory(folder);
        var final = PathOf(model);
        var part = final + ".part";
        long have = File.Exists(part) ? new FileInfo(part).Length : 0;
        if (have > model.Bytes) { File.Delete(part); have = 0; }
        if (have < model.Bytes)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, model.Url);
            if (have > 0) request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(have, null);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
            if (have > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent) { have = 0; File.Delete(part); }   // the server ignored the range: from the start
            if (!response.IsSuccessStatusCode) throw new IOException("Não foi possível baixar o modelo (" + (int)response.StatusCode + ").");
            await using var body = await response.Content.ReadAsStreamAsync(cancel);
            await using var file = new FileStream(part, have > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
            var buffer = new byte[1 << 20];
            long done = have;
            progress?.Report((done, model.Bytes));
            int read;
            while ((read = await body.ReadAsync(buffer, cancel)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancel);
                done += read;
                progress?.Report((done, model.Bytes));
            }
            await file.FlushAsync(cancel);
        }
        if (new FileInfo(part).Length != model.Bytes) throw new IOException("O download terminou incompleto. Tente de novo para continuar de onde parou.");
        // the file is the one that was meant, or nothing
        string hash;
        await using (var check = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true))
            hash = Convert.ToHexString(await SHA256.HashDataAsync(check, cancel)).ToLowerInvariant();
        if (!string.Equals(hash, model.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(part);
            throw new IOException("O arquivo baixado não confere com o esperado e foi descartado. Tente de novo.");
        }
        File.Move(part, final, overwrite: true);
    }
}
