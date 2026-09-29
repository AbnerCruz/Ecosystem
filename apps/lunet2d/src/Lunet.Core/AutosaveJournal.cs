using System.Security.Cryptography;
using System.Text;

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
    private const string Extension = ".buf";
    private const int MaxHistory = 5;
    private readonly LunetProject _project;
    private readonly string _directory;

    public AutosaveJournal(LunetProject project)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _directory = System.IO.Path.Combine(project.Directory, ".lunet", "autosave");
    }

    /// <summary>Guarda o texto atual de <paramref name="relativePath"/> como buffer de trabalho.</summary>
    public void WriteBuffer(string relativePath, string text)
    {
        _project.EnsureInsideProject(relativePath);
        Directory.CreateDirectory(_directory);
        var payload = relativePath + "\n" + text;
        AtomicFile.WriteAllText(BufferPath(relativePath), payload);
        var history = HistoryFiles(relativePath);
        if (history.Count > 0 && File.ReadAllText(history[0].Path) == payload) return;
        var ticks = DateTime.UtcNow.Ticks;
        var snapshot = SnapshotPath(relativePath, ticks);
        while (File.Exists(snapshot)) snapshot = SnapshotPath(relativePath, ++ticks);
        AtomicFile.WriteAllText(snapshot, payload);
        foreach (var old in HistoryFiles(relativePath).Skip(MaxHistory)) File.Delete(old.Path);
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
            var newline = content.IndexOf('\n');
            if (newline < 0 || content[..newline] != recovery.Path) continue;
            var text = content[(newline + 1)..];
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

            var newline = content.IndexOf('\n');
            if (newline < 0) { TryDelete(file); continue; } // buffer truncado: inútil
            var relative = content[..newline];
            var text = content[(newline + 1)..];

            string? disk = null;
            try { disk = _project.ReadText(relative); }
            catch (Exception ex) when (ex is IOException or ProjectException) { }

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

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { }
    }
}
