using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// Narrow, file-bound authority for an exported Atlas Map V1 document. No arbitrary vault filesystem
/// operations are exposed to JavaScript: a session can open existing .md paths and save only its own HTML.
/// Created once per content viewer, never shared across files, frames or vault selections.
/// </summary>
public sealed class AtlasMapSession
{
    public const int MaximumBytes = 32 * 1024 * 1024;
    private static readonly Regex DataScript = new(
        "<script\\s+type=[\\"']application/json[\\"']\\s+id=[\\"']atlas-map-data[\\"']\\s*>(.*?)</script\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));
    private readonly IUrbeVaultStorage _vault;
    private readonly Func<string, Task<bool>>? _openNote;
    private readonly string _vaultIdentity;
    private readonly string _path;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private byte[] _baseHash;
    private long _revision;
    private int _closed;

    private AtlasMapSession(ContentRequest request, string id, long revision, byte[] original)
    {
        _vault = request.Storage!;
        _vaultIdentity = _vault.VaultIdentity!;
        _path = request.Path;
        _openNote = request.AtlasOpenNote;
        MapId = id;
        _revision = revision;
        _baseHash = SHA256.HashData(original);
    }

    public string MapId { get; }

    public static AtlasMapSession? TryCreate(ContentRequest request)
    {
        if (request.Kind != VaultFileKinds.Html || request.Storage is not { IsConnected: true, VaultIdentity: not null } ||
            !request.Path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            request.Bytes is not { Length: > 0 and <= MaximumBytes } bytes)
            return null;
        try { IUrbeVaultStorage.EnsureWritableTextPath(request.Path); }
        catch (Exception) { return null; }
        var html = Encoding.UTF8.GetString(bytes);
        return TryParse(html, out var id, out var revision) ? new AtlasMapSession(request, id, revision, bytes) : null;
    }

    /// <summary>Read Atlas data from the actual exported HTML, not from the page title or file extension.</summary>
    public static bool TryParse(string html, out string mapId, out long revision)
    {
        mapId = "";
        revision = 0;
        if (string.IsNullOrEmpty(html) || Encoding.UTF8.GetByteCount(html) > MaximumBytes) return false;
        try
        {
            var match = DataScript.Match(html);
            if (!match.Success) return false;
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            var obj = doc.RootElement;
            if (obj.ValueKind != JsonValueKind.Object ||
                !obj.TryGetProperty("format", out var format) || format.GetString() != "atlas-map" ||
                !obj.TryGetProperty("version", out var version) || version.GetInt32() != 1 ||
                !obj.TryGetProperty("mapId", out var id) || id.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(id.GetString()) || id.GetString()!.Length > 160 ||
                !obj.TryGetProperty("revision", out var rev) || rev.ValueKind != JsonValueKind.Number ||
                !rev.TryGetInt64(out revision) || revision < 0 ||
                !obj.TryGetProperty("project", out var project) || project.ValueKind != JsonValueKind.Object ||
                !obj.TryGetProperty("annotations", out var annotations) || annotations.ValueKind != JsonValueKind.Object ||
                !annotations.TryGetProperty("type", out var type) || type.GetString() != "FeatureCollection" ||
                !annotations.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
                return false;
            mapId = id.GetString()!;
            return true;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or RegexMatchTimeoutException or FormatException)
        {
            mapId = "";
            revision = 0;
            return false;
        }
    }

    /// <summary>Only exact vault-relative Markdown paths; #anchor is accepted but navigation remains note-level in V1.</summary>
    public static bool TryNotePath(string? raw, out string path)
    {
        path = "";
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 1024 || raw.Contains('%') ||
            raw.Contains('?') || raw.Contains('\\') || raw.Contains(':') || raw.StartsWith('/') ||
            raw.Any(char.IsControl) || !raw.IsNormalized(NormalizationForm.FormC))
            return false;
        var hash = raw.IndexOf('#');
        var candidate = hash < 0 ? raw : raw[..hash];
        if (hash >= 0 && (raw.IndexOf('#', hash + 1) >= 0 || hash == raw.Length - 1)) return false;
        if (!candidate.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            candidate.Split('/').Any(s => s.Length == 0 || s is "." or ".." || s.StartsWith('.') || s.EndsWith(' ') || s.EndsWith('.')))
            return false;
        path = candidate;
        return true;
    }

    private bool Active => Volatile.Read(ref _closed) == 0 &&
                           _vault.IsConnected && string.Equals(_vault.VaultIdentity, _vaultIdentity, StringComparison.Ordinal);

    public async Task<bool> OpenNoteAsync(string mapId, string path)
    {
        if (!Active || mapId != MapId || _openNote is null || !TryNotePath(path, out var note)) return false;
        try { return await _openNote(note); }
        catch (Exception) { return false; }
    }

    public sealed record SaveResult(bool Ok, string MapId, long Revision, string? Error = null);

    /// <summary>Compare and save within the vault writer's own lock; never trust Atlas revision as a disk version.</summary>
    public async Task<SaveResult> SaveAsync(string mapId, long revision, string html, CancellationToken cancellationToken = default)
    {
        if (!Active || mapId != MapId || revision <= _revision ||
            !TryParse(html, out var actualId, out var actualRevision) ||
            actualId != MapId || actualRevision != revision)
            return new SaveResult(false, MapId, revision, "Identidade, versão ou conteúdo do mapa inválido.");
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            if (!Active || revision <= _revision)
                return new SaveResult(false, MapId, revision, "Sessão ou revisão desatualizada.");
            // The storage checks this hash under its own lock, then writes and verifies the exact user file.
            await _vault.SaveExistingTextFileIfUnchangedAsync(_path, html, _baseHash, cancellationToken);
            var after = await _vault.ReadFileAsync(_path, MaximumBytes, cancellationToken);
            if (after?.Bytes is not { } actual || !Encoding.UTF8.GetString(actual).TrimStart('\uFEFF').Equals(html, StringComparison.Ordinal))
                return new SaveResult(false, MapId, revision, "Gravação não passou na verificação de leitura.");
            _baseHash = SHA256.HashData(actual);
            _revision = revision;
            return new SaveResult(true, MapId, revision);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or
                                      UnauthorizedAccessException or NotSupportedException or OperationCanceledException)
        {
            return new SaveResult(false, MapId, revision, e.Message);
        }
        finally { _saveGate.Release(); }
    }

    public void Revoke() => Interlocked.Exchange(ref _closed, 1);
}
