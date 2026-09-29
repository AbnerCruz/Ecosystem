using System.Security.Cryptography;
using System.Text;

namespace Lunet.Core;

/// <summary>Alteração não salva encontrada após um encerramento inesperado.</summary>
public sealed record Recovery(string Path, string RecoveredText, string? DiskText, DateTime BufferTimeUtc);

/// <summary>
/// Working buffers em <c>.lunet/autosave/</c>. O editor grava o texto atual do arquivo aberto a cada pausa na digitação;
/// o buffer é apagado quando o arquivo é salvo de verdade. Se o app morrer, os buffers que sobram são ofertados na próxima abertura.
/// Toda gravação é atômica (arquivo temporário + rename).
/// </summary>
public sealed class AutosaveJournal
{
    private const string Extension = ".buf";
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
    }

    /// <summary>Apaga o buffer (chamado depois de salvar o arquivo com sucesso).</summary>
    public void Discard(string relativePath)
    {
        var path = BufferPath(relativePath);
        if (File.Exists(path)) File.Delete(path);
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

            if (disk == text) { TryDelete(file); continue; }
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
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath)))[..24];
        return System.IO.Path.Combine(_directory, hash + Extension);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { }
    }
}
