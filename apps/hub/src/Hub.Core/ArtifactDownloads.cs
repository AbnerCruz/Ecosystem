using System.Net;
using System.Security.Cryptography;

namespace Hub.Core;

/// <summary>Seleção derivada do catálogo, nunca de um nome/caminho digitado pelo usuário.</summary>
public sealed record ArtifactChoice(string ProductId, string ProductName, ReleaseChannel Channel,
    string Tag, ReleaseAssetInfo Asset, bool Stale);

public enum ArtifactDownloadState { Verified, Failed, Cancelled }
public enum ArtifactDownloadError
{
    None, InvalidMetadata, MissingChecksum, TooLarge, Http, SizeMismatch, ChecksumMismatch, Network, Timeout, Storage, BackgroundUnavailable, Interrupted
}

/// <summary>LocalPath só existe após tamanho e SHA-256 dos bytes serem conferidos; não prova assinatura/compatibilidade.</summary>
public sealed record ArtifactDownloadResult(ArtifactDownloadState State, ArtifactDownloadError Error,
    string Message, string? LocalPath = null, long Bytes = 0, string? Sha256 = null);

public sealed record ArtifactDownloadProgress(long Bytes, long ExpectedBytes);

public static class ArtifactCatalog
{
    public static IReadOnlyList<ArtifactChoice> Choices(HubSnapshot? snapshot)
    {
        var choices = new List<ArtifactChoice>();
        if (snapshot?.ReleaseChannels is null) return choices;
        foreach (var product in snapshot.Products)
        {
            if (!snapshot.ReleaseChannels.TryGetValue(product.Id, out var channel) || channel?.Value is null ||
                channel.Availability == Availability.NotAvailable ||
                !snapshot.ProductReleases.TryGetValue(product.Id, out var releases) || releases?.Value is null ||
                releases.Availability == Availability.NotAvailable) continue;
            foreach (var release in releases.Value.Where(r => r is not null).Take(3))
                foreach (var asset in (release.Artifacts ?? []).Where(a => a is not null && a.IsAndroidApk).Take(4))
                    choices.Add(new(product.Id, product.Name.Value ?? product.Id, channel.Value, release.Tag, asset,
                        snapshot.Stale || channel.Availability == Availability.Stale || releases.Availability == Availability.Stale));
        }
        return choices;
    }
}

/// <summary>Download local do Hub: GET público, streaming limitado, arquivo parcial e publicação atômica após verificação.</summary>
public sealed class ArtifactDownloader(HttpClient http, string privateDirectory,
    long maxBytes = ArtifactDownloader.DefaultMaxBytes, TimeSpan? timeout = null)
{
    public const long DefaultMaxBytes = 128L * 1024 * 1024;
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    readonly SemaphoreSlim _gate = new(1, 1);

    public static ArtifactDownloadResult? Ineligible(ArtifactChoice choice, long limit = DefaultMaxBytes)
    {
        if (choice.Asset is not { } a || choice.Channel?.Repository is not { } repo ||
            string.IsNullOrWhiteSpace(repo.Owner) || string.IsNullOrWhiteSpace(repo.Name) ||
            repo.Owner is "." or ".." || repo.Name is "." or ".." ||
            repo.Owner.IndexOfAny(['/', '\\']) >= 0 || repo.Name.IndexOfAny(['/', '\\']) >= 0 ||
            !a.IsAndroidApk || a.Size <= 0 || string.IsNullOrWhiteSpace(choice.Tag) ||
            a.Name.IndexOfAny(['/', '\\']) >= 0 || choice.Tag.Split('/').Any(p => p is "." or ".." or "") ||
            (choice.Channel.TagPrefix is { } prefix && !choice.Tag.StartsWith(prefix, StringComparison.Ordinal)) ||
            !Uri.TryCreate(a.Url, UriKind.Absolute, out var actual) || actual.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(actual.UserInfo) || !string.IsNullOrEmpty(actual.Query) || !string.IsNullOrEmpty(actual.Fragment))
            return Failed(ArtifactDownloadError.InvalidMetadata, "APK ou canal inválido para download.");
        // A primeira URL deve ser o asset daquele repositório/tag, sem URL externa arbitrária vinda do cache/API.
        var parts = actual.AbsolutePath.Split('/').Select(Uri.UnescapeDataString).ToArray();
        if (!actual.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || actual.Port != 443 || parts.Length != 7 ||
            !parts[1].Equals(repo.Owner, StringComparison.OrdinalIgnoreCase) || !parts[2].Equals(repo.Name, StringComparison.OrdinalIgnoreCase) ||
            parts[3] != "releases" || parts[4] != "download" || parts[5] != choice.Tag || parts[6] != a.Name)
            return Failed(ArtifactDownloadError.InvalidMetadata, "URL do APK não corresponde ao canal e à release declarados.");
        if (limit <= 0 || a.Size > limit)
            return Failed(ArtifactDownloadError.TooLarge, "APK excede o limite de download.");
        if (a.Sha256 is null || a.Sha256.Length != 64 || !a.Sha256.All(Uri.IsHexDigit))
            return Failed(ArtifactDownloadError.MissingChecksum, "SHA-256 ausente ou inválido; download indisponível.");
        return null;
    }

    public async Task<ArtifactDownloadResult> DownloadAsync(ArtifactChoice choice,
        IProgress<ArtifactDownloadProgress>? progress = null, CancellationToken ct = default)
    {
        if (Ineligible(choice, maxBytes) is { } invalid) return invalid;
        // Este transporte não recebe token. Evita herdar Authorization configurado por outro consumidor.
        if (http.DefaultRequestHeaders.Authorization is not null || http.DefaultRequestHeaders.Contains("Cookie") ||
            http.DefaultRequestHeaders.Contains("Proxy-Authorization"))
            return Failed(ArtifactDownloadError.InvalidMetadata, "O download exige um cliente público sem credencial.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout ?? DefaultTimeout);
        var partial = Path.Combine(privateDirectory, $"hub-{Guid.NewGuid():N}.part");
        bool acquired = false;
        bool cleanupFailed = false;
        ArtifactDownloadResult result;
        try
        {
            await _gate.WaitAsync(deadline.Token).ConfigureAwait(false);
            acquired = true;
            result = await TransferAsync(choice, partial, progress, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        { result = new(ArtifactDownloadState.Cancelled, ArtifactDownloadError.None, "Download cancelado; arquivo incompleto descartado."); }
        catch (OperationCanceledException)
        { result = Failed(ArtifactDownloadError.Timeout, "Tempo de download esgotado; arquivo incompleto descartado."); }
        catch (HttpRequestException)
        { result = Failed(ArtifactDownloadError.Network, "Falha de rede; arquivo incompleto descartado."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { result = Failed(ArtifactDownloadError.Storage, "Falha ao ler/gravar o download; arquivo incompleto descartado."); }
        finally
        {
            try { if (File.Exists(partial)) File.Delete(partial); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { cleanupFailed = true; }
            if (acquired) _gate.Release();
        }
        return cleanupFailed ? Failed(ArtifactDownloadError.Storage, "Falha ao descartar o arquivo incompleto na área privada.") : result;
    }

    async Task<ArtifactDownloadResult> TransferAsync(ArtifactChoice choice, string partial,
        IProgress<ArtifactDownloadProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(privateDirectory);
        // Recupera parciais abandonados por encerramento do processo; nunca usa o nome remoto como caminho local.
        foreach (var abandoned in Directory.EnumerateFiles(privateDirectory, "hub-*.part")) File.Delete(abandoned);
        using var request = new HttpRequestMessage(HttpMethod.Get, choice.Asset.Url);
        request.Headers.UserAgent.ParseAdd("ecosystem-hub-download");
        request.Headers.AcceptEncoding.ParseAdd("identity");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
            return Failed(ArtifactDownloadError.Http, $"Download indisponível: HTTP {(int)response.StatusCode}.");
        if (response.RequestMessage?.RequestUri is { } final && final.Scheme != Uri.UriSchemeHttps)
            return Failed(ArtifactDownloadError.InvalidMetadata, "O download foi redirecionado para um endereço sem HTTPS.");
        if (response.Content.Headers.ContentLength is { } length && length != choice.Asset.Size)
            return Failed(ArtifactDownloadError.SizeMismatch, "Tamanho HTTP diverge do catálogo; arquivo rejeitado.");
        if (response.Content.Headers.ContentEncoding.Any(e => !e.Equals("identity", StringComparison.OrdinalIgnoreCase)))
            return Failed(ArtifactDownloadError.InvalidMetadata, "Resposta compactada inesperada; arquivo rejeitado.");
        await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long bytes = 0;
        await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
        {
            var buffer = new byte[65536];
            int read;
            while (true)
            {
                // Lê no máximo o tamanho esperado, mais um byte para detectar excesso; não drena resposta sem limite.
                long remaining = Math.Min(maxBytes, choice.Asset.Size) - bytes;
                int requested = remaining > 0 ? (int)Math.Min(buffer.Length, remaining) : 1;
                read = await input.ReadAsync(buffer.AsMemory(0, requested), ct).ConfigureAwait(false);
                if (read == 0) break;
                bytes += read;
                if (bytes > maxBytes) return Failed(ArtifactDownloadError.TooLarge, "Download excedeu o limite; arquivo descartado.");
                if (bytes > choice.Asset.Size) return Failed(ArtifactDownloadError.SizeMismatch, "APK maior que o catálogo; arquivo descartado.");
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                progress?.Report(new(bytes, choice.Asset.Size));
            }
            if (bytes != choice.Asset.Size)
                return Failed(ArtifactDownloadError.SizeMismatch, "APK incompleto ou de tamanho diferente; arquivo descartado.");
            var digest = hash.GetHashAndReset();
            if (!CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(choice.Asset.Sha256!)))
                return Failed(ArtifactDownloadError.ChecksumMismatch, "SHA-256 diverge do catálogo; arquivo descartado.");
            await output.FlushAsync(ct).ConfigureAwait(false);
        }
        ct.ThrowIfCancellationRequested();
        var verified = Path.Combine(privateDirectory, "hub-verified.apk");
        File.Move(partial, verified, overwrite: true);
        return new(ArtifactDownloadState.Verified, ArtifactDownloadError.None,
            "APK baixado: tamanho e SHA-256 conferidos. Instalação ainda não disponível.", verified, bytes, choice.Asset.Sha256!.ToLowerInvariant());
    }

    static ArtifactDownloadResult Failed(ArtifactDownloadError error, string message) => new(ArtifactDownloadState.Failed, error, message);
}
