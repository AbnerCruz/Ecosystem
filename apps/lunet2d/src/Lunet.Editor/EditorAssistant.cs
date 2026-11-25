using Lunet.Compiler;

namespace Lunet.Editor;

/// <summary>Resultado de uma análise para a posição do cursor.</summary>
public sealed record Analysis(int Version, IReadOnlyList<CompletionItem> Completions, IReadOnlyList<LunetDiagnostic> Diagnostics, IReadOnlyList<FoldRegion> Folds, CallSignatureHelp? Signature);

/// <summary>
/// Serializa o acesso ao <see cref="CodeAnalyzer"/> (que não é thread-safe) e roda a análise fora da thread de UI.
/// Portátil (LUNET-536 M1): usado pela casca Avalonia e pelas telas Android anteriores.
/// </summary>
public sealed class EditorAssistant
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _revisions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (IReadOnlyList<LunetDiagnostic> Diagnostics, IReadOnlyList<FoldRegion> Folds)> _fileAnalysis = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _sources = new(StringComparer.Ordinal);
    private Task _loading = Task.CompletedTask;
    private readonly CodeAnalyzer _analyzer;

    /// <param name="references">The assemblies games compile against (the Framework, at least).</param>
    public EditorAssistant(IReferenceProvider references) => _analyzer = new CodeAnalyzer(references);

    public Task LoadProjectAsync(IEnumerable<(string Path, string Text)> files) => _loading = Task.Run(() =>
    {
        lock (_gate) foreach (var (path, text) in files) SetSnapshot(path, text);
    });

    public void SetFile(string path, string text)
    {
        lock (_gate) SetSnapshot(path, text);
    }

    public void RemoveFile(string path)
    {
        lock (_gate) { _sources.Remove(path); _analyzer.RemoveFile(path); _fileAnalysis.Clear(); }
    }

    public Task<Analysis> AnalyzeAsync(string path, string text, int caret, int version, long revision, bool complete,
        CancellationToken token) => RunAsync(a =>
    {
        token.ThrowIfCancellationRequested();
        UpdateSnapshot(path, text, revision);
        var help = caret >= 0 ? a.GetCallSignatures(path, caret) : null;
        var completions = complete && ShouldComplete(text, caret) ? a.GetCompletions(path, caret) : [];
        token.ThrowIfCancellationRequested();
        if (!_fileAnalysis.TryGetValue(path, out var cached))
            _fileAnalysis[path] = cached = (a.GetDiagnostics(path), a.GetFoldRegions(path));
        return new Analysis(version, completions, cached.Diagnostics, cached.Folds, help);
    });

    public void UpdateSnapshot(string path, string text, long revision)
    {
        if (_revisions.TryGetValue(path, out var latest) && latest >= revision) return;
        _revisions[path] = revision;
        SetSnapshot(path, text);
    }

    private void SetSnapshot(string path, string text)
    {
        if (_sources.TryGetValue(path, out var previous) && previous == text) return;
        _sources[path] = text;
        _analyzer.SetFile(path, text);
        _fileAnalysis.Clear();
    }

    public async Task<T> RunAsync<T>(Func<CodeAnalyzer, T> action)
    {
        await _loading.ConfigureAwait(false);
        return await Task.Run(() =>
        {
            lock (_gate) return action(_analyzer);
        }).ConfigureAwait(false);
    }

    /// <summary>Só sugere depois de letra, dígito, '_' ou '.'.</summary>
    private static bool ShouldComplete(string text, int caret) =>
        caret > 0 && caret <= text.Length && (char.IsLetterOrDigit(text[caret - 1]) || text[caret - 1] is '_' or '.');
}
