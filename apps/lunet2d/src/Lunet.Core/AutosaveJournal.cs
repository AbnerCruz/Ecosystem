using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lunet.Core;

/// <summary>Alteração não salva encontrada após um encerramento inesperado.</summary>
public sealed record Recovery(string Path, string RecoveredText, string? DiskText, DateTime BufferTimeUtc);

/// <summary>
/// Working buffers em <c>.lunet/autosave/</c>. O editor grava o texto atual do arquivo aberto a cada pausa na digitação;
/// o buffer é apagado quando o arquivo é salvo de verdade. Se o app morrer, os buffers que sobram são ofertados na próxima abertura.
/// Um histórico limitado das últimas cinco versões permite escolher uma versão anterior à interrupção.
/// Toda gravação é atômica (arquivo temporário + rename).
/// </summary>
public sealed class AutosaveJournal
{
    public sealed record Draft(string Path, string Text, string? Previous);
    private const string Extension = ".buf";
    private const int MaxHistory = 5;
    private readonly LunetProject _project;
    private readonly string _directory;
    private readonly bool _legacy;

    public AutosaveJournal(LunetProject project, string? privateRoot = null)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _legacy = privateRoot is null;
        _directory = privateRoot is null
            ? Path.Combine(project.Directory, ".lunet", "autosave")
            : Path.Combine(privateRoot, FileKey(project.Manifest.GameId + "\n" + project.Files.Location));
    }

    /// <summary>Guarda o texto atual de <paramref name="relativePath"/> como buffer de trabalho.</summary>
    public void WriteBuffer(string relativePath, string text, string? previous = null)
    {
        _project.EnsureInsideProject(relativePath);
        Directory.CreateDirectory(_directory);
        var payload = previous is null ? relativePath + "\n" + text : JsonSerializer.Serialize(new Draft(relativePath, text, previous));
        AtomicFile.WriteAllText(BufferPath(relativePath), payload);
        var history = HistoryFiles(relativePath);
        if (history.Count > 0 && File.ReadAllText(history[0].Path) == payload) return;
        var ticks = DateTime.UtcNow.Ticks;
        var snapshot = SnapshotPath(relativePath, ticks);
        while (File.Exists(snapshot)) snapshot = SnapshotPath(relativePath, ++ticks);
        AtomicFile.WriteAllText(snapshot, payload);
        foreach (var old in HistoryFiles(relativePath).Skip(MaxHistory)) File.Delete(old.Path);
    }

    public Draft? ReadBuffer(string path)
    {
        var file = BufferPath(path);
        if (!File.Exists(file)) return null;
        var draft = Decode(File.ReadAllText(file));
        return draft?.Path == path ? draft : null;
    }

    private static Draft? Decode(string payload)
    {
        if (payload.StartsWith('{'))
        {
            try { return JsonSerializer.Deserialize<Draft>(payload); }
            catch (JsonException) { /* legacy file names can start with '{' */ }
        }
        var newline = payload.IndexOf('\n');
        return newline < 0 ? null : new Draft(payload[..newline], payload[(newline + 1)..], null);
    }

    /// <summary>Preserves the buffer if the write fails or the source changed since opening.</summary>
    public void SaveVerified(string path, string previous, string text)
    {
        WriteBuffer(path, text, previous);
        if (!_project.WriteTextIfUnchanged(path, previous, text, out var actual) && actual != text)
            throw new IOException("O arquivo foi alterado fora do editor. O rascunho foi preservado.");
        if (_project.ReadText(path) != text)
            throw new IOException("O arquivo gravado difere do rascunho.");
        Discard(path);
    }

    /// <summary>Apaga o buffer (chamado depois de salvar o arquivo com sucesso).</summary>
    public void Discard(string relativePath)
    {
        var path = BufferPath(relativePath);
        if (File.Exists(path)) File.Delete(path);
        foreach (var snapshot in HistoryFiles(relativePath)) File.Delete(snapshot.Path);
    }

    /// <summary>Versões recentes de um buffer recuperável, da mais nova à mais antiga.</summary>
    public IReadOnlyList<Recovery> FindHistory(Recovery recovery)
    {
        _project.EnsureInsideProject(recovery.Path);
        var versions = new List<Recovery> { recovery };
        foreach (var (file, time) in HistoryFiles(recovery.Path))
        {
            string content;
            try { content = File.ReadAllText(file); }
            catch (IOException) { continue; }
            var draft = Decode(content);
            if (draft?.Path != recovery.Path) continue;
            var text = draft.Text;
            if (versions.Any(v => v.RecoveredText == text)) continue;
            versions.Add(new Recovery(recovery.Path, text, recovery.DiskText, time));
        }
        return versions;
    }

    /// <summary>Buffers cujo conteúdo difere do arquivo em disco; buffers idênticos ao disco são descartados.</summary>
    public IReadOnlyList<Recovery> FindRecoveries()
    {
        if (!Directory.Exists(_directory)) return [];
        var result = new List<Recovery>();
        foreach (var file in Directory.EnumerateFiles(_directory, "*" + Extension).ToList())
        {
            string content;
            try { content = File.ReadAllText(file); }
            catch (IOException) { continue; }

            var draft = Decode(content);
            if (draft is null)
            {
                if (_legacy)
                {
                    try { File.Delete(file); }
                    catch (IOException) { /* Cleanup can be retried. */ }
                }
                continue;
            }
            var relative = draft.Path;
            var text = draft.Text;

            string? disk = null;
            try { disk = _project.ReadText(relative); }
            catch (Exception ex) when (ex is IOException or ProjectException)
            {
                // Sem ler o arquivo no disco, a versão do diário é oferecida para recuperação.
            }

            if (disk == text) { Discard(relative); continue; }
            result.Add(new Recovery(relative, text, disk, File.GetLastWriteTimeUtc(file)));
        }
        return result.OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Aplica a recuperação: grava o texto recuperado no arquivo e descarta o buffer.</summary>
    public void Restore(Recovery recovery)
    {
        _project.WriteText(recovery.Path, recovery.RecoveredText);
        Discard(recovery.Path);
    }

    private string BufferPath(string relativePath)
    {
        return System.IO.Path.Combine(_directory, FileKey(relativePath) + Extension);
    }

    private static string FileKey(string relativePath) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath)))[..24];

    private string SnapshotPath(string relativePath, long ticks) =>
        System.IO.Path.Combine(_directory, FileKey(relativePath) + ".snapshot." + ticks.ToString("D19"));

    private IReadOnlyList<(string Path, DateTime Time)> HistoryFiles(string relativePath)
    {
        if (!Directory.Exists(_directory)) return [];
        var prefix = FileKey(relativePath) + ".snapshot.";
        return Directory.EnumerateFiles(_directory, prefix + "*")
            .Select(path => (Path: path, Name: System.IO.Path.GetFileName(path)))
            .Where(item => item.Name.StartsWith(prefix, StringComparison.Ordinal) &&
                           long.TryParse(item.Name[prefix.Length..], out _))
            .OrderByDescending(item => item.Name, StringComparer.Ordinal)
            .Select(item => (item.Path, Time: File.GetLastWriteTimeUtc(item.Path)))
            .ToList();
    }

}
