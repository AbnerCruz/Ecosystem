using Lunet.Compiler;
using Lunet.Editor;

namespace Lunet.Android.Editor;

/// <summary>Resultado de uma análise para a posição do cursor.</summary>
internal sealed record Analysis(int Version, IReadOnlyList<CompletionItem> Completions, IReadOnlyList<LunetDiagnostic> Diagnostics);

/// <summary>Serializa o acesso ao <see cref="CodeAnalyzer"/> (que não é thread-safe) e roda a análise fora da thread de UI.</summary>
internal sealed class EditorAssistant
{
    private readonly object _gate = new();
    private readonly CodeAnalyzer _analyzer = new(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));

    public Task LoadProjectAsync(IEnumerable<(string Path, string Text)> files) => Task.Run(() =>
    {
        lock (_gate) foreach (var (path, text) in files) _analyzer.SetFile(path, text);
    });

    public void SetFile(string path, string text)
    {
        lock (_gate) _analyzer.SetFile(path, text);
    }

    public Task<Analysis> AnalyzeAsync(string path, string text, int caret, int version) => Task.Run(() =>
    {
        lock (_gate)
        {
            _analyzer.SetFile(path, text);
            var completions = ShouldComplete(text, caret) ? _analyzer.GetCompletions(path, caret) : [];
            return new Analysis(version, completions, _analyzer.GetDiagnostics(path));
        }
    });

    public Task<T> RunAsync<T>(Func<CodeAnalyzer, T> action) => Task.Run(() =>
    {
        lock (_gate) return action(_analyzer);
    });

    /// <summary>Só sugere depois de letra, dígito, '_' ou '.'.</summary>
    private static bool ShouldComplete(string text, int caret) =>
        caret > 0 && caret <= text.Length && (char.IsLetterOrDigit(text[caret - 1]) || text[caret - 1] is '_' or '.');
}
