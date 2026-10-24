using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using Urbe.Core.Assistant;

namespace Urbe.LocalAi;

/// <summary>
/// The offline text model: Gemma 4 on llama.cpp (LLamaSharp). The weights are loaded the first time they are needed and
/// kept for the next question; <see cref="Unload"/> gives the memory back (the app does it when it goes to the background).
/// One answer at a time. The prompt is Google's own chat format (<see cref="Gemma4Chat"/>), tools included.
/// </summary>
public sealed class LlamaTextRuntime : ILocalTextRuntime, IPartialDownloads, IDisposable
{
    private readonly ModelStore _store;
    private readonly long _ramBytes;
    private readonly SemaphoreSlim _one = new(1, 1);
    private LLamaWeights? _weights;
    private string? _loaded;
    private ModelParams? _params;
    // what the context has already read: the next request pays only for what is new after the part it shares (the system
    // prompt and the tools are the same in every request; an agent's second step costs a few hundred tokens, not thousands)
    private LLamaContext? _context;
    private readonly List<LLamaToken> _cached = [];

    /// <param name="folder">Where the model files are kept (the app's own data, not the vault).</param>
    /// <param name="totalRamBytes">The device's memory, to recommend the model that fits.</param>
    public LlamaTextRuntime(string folder, long totalRamBytes, HttpClient? http = null)
    {
        _store = new ModelStore(folder, http);
        _ramBytes = totalRamBytes;
    }

    /// <summary>Where the native llama libraries are when the system puts them apart from the app (Android: the app's native library folder).</summary>
    public static void UseNativeFolder(string folder) => LLama.Native.NativeLibraryConfig.All.WithSearchDirectory(folder);

    public IReadOnlyList<LocalModel> Models => LocalModelCatalog.All;

    public LocalModel Recommended => LocalModelCatalog.For(_ramBytes);

    public bool IsInstalled(LocalModel model) => _store.IsInstalled(model);

    public long PartialBytes(LocalModel model) => _store.PartialBytes(model);

    public Task InstallAsync(LocalModel model, IProgress<(long Done, long Total)>? progress, CancellationToken cancel) =>
        _store.InstallAsync(model, progress, cancel);

    public void Remove(LocalModel model)
    {
        if (_loaded == model.Id) Unload();
        _store.Remove(model);
    }

    /// <summary>Gives back the memory of the loaded model.</summary>
    public void Unload()
    {
        _context?.Dispose();
        _context = null;
        _cached.Clear();
        _weights?.Dispose();
        _weights = null;
        _loaded = null;
        _params = null;
    }

    public void Dispose()
    {
        Unload();
        _one.Dispose();
    }

    private async Task<LLamaWeights> LoadAsync(LocalModel model, CancellationToken cancel)
    {
        if (_weights is not null && _loaded == model.Id) return _weights;
        Unload();
        if (!_store.IsInstalled(model)) throw new AiProviderException("O modelo " + model.Name + " ainda não foi baixado. Baixe-o em Assistente → IA no aparelho.");
        _params = new ModelParams(_store.PathOf(model))
        {
            ContextSize = (uint)model.Context,
            GpuLayerCount = 0,
            Threads = Math.Clamp(Environment.ProcessorCount - 1, 2, 8),
            BatchThreads = Math.Clamp(Environment.ProcessorCount - 1, 2, 8),
            BatchSize = 512
        };
        _weights = await LLamaWeights.LoadFromFileAsync(_params, cancel);
        _loaded = model.Id;
        return _weights;
    }

    public async Task<AiResult> ChatAsync(LocalModel model, AiRequest request, Action<AiStreamEvent> onEvent, CancellationToken cancel)
    {
        await _one.WaitAsync(cancel);
        try
        {
            var weights = await LoadAsync(model, cancel);
            var prompt = Gemma4Chat.Render(request);
            // the tokenizer puts the start-of-text token itself
            if (prompt.StartsWith("<bos>", StringComparison.Ordinal)) prompt = prompt["<bos>".Length..];
            _context ??= weights.CreateContext(_params!);
            var context = _context;
            var tokens = context.Tokenize(prompt, addBos: true, special: true);
            int max = request.MaxTokens ?? 2048;
            if (tokens.Length + max > (int)context.ContextSize)
            {
                max = Math.Min(max, (int)context.ContextSize - tokens.Length);
                if (max < 64) throw new AiProviderException("A conversa ficou grande demais para a IA no aparelho. Comece uma nova conversa.");
            }

            // keep what the model has already read: only the new end of the prompt is read again
            int common = 0;
            while (common < _cached.Count && common < tokens.Length && _cached[common] == tokens[common]) common++;
            if (common == tokens.Length) common--;   // the last token is read again: its result starts the answer
            if (common > 0)
            {
                context.NativeHandle.MemorySequenceRemove(LLamaSeqId.Zero, common, -1);
                // the cache may refuse to cut in the middle (it tells nothing): then everything is read again
                if ((int)context.NativeHandle.MemorySequenceMaxPosition(LLamaSeqId.Zero) != common - 1) common = 0;
            }
            if (common == 0) context.NativeHandle.MemoryClear(true);
            _cached.RemoveRange(common, _cached.Count - common);

            var batch = new LLamaBatch();
            int logitsAt = 0;
            for (int at = common; at < tokens.Length; at += 512)
            {
                batch.Clear();
                int end = Math.Min(tokens.Length, at + 512);
                for (int i = at; i < end; i++) batch.Add(tokens[i], i, LLamaSeqId.Zero, i == tokens.Length - 1);
                logitsAt = batch.TokenCount - 1;
                if (await context.DecodeAsync(batch, cancel) != DecodeResult.Ok) { _cached.Clear(); throw new AiProviderException("A IA no aparelho não conseguiu ler a conversa."); }
                for (int i = at; i < end; i++) _cached.Add(tokens[i]);
            }

            var sampling = new DefaultSamplingPipeline { Temperature = (float)(request.Temperature ?? .7), TopK = 64, TopP = .95f, MinP = 0f };
            var decoder = new StreamingTokenDecoder(context);
            var streamer = new Gemma4Chat.Streamer();
            var output = new System.Text.StringBuilder();
            int made = 0;
            bool stopped = false;
            int position = tokens.Length;
            while (made < max && !stopped)
            {
                cancel.ThrowIfCancellationRequested();
                var token = sampling.Sample(context.NativeHandle, logitsAt);
                if (token.IsEndOfGeneration(weights.Vocab)) break;
                made++;
                decoder.Add(token);
                var piece = decoder.Read();
                if (piece.Length > 0)
                {
                    output.Append(piece);
                    var text = output.ToString();
                    var stopAt = Gemma4Chat.StopTexts.Select(t => text.IndexOf(t, StringComparison.Ordinal)).Where(i => i >= 0).DefaultIfEmpty(-1).Min();
                    if (stopAt >= 0) { output.Length = stopAt; stopped = true; piece = ""; }
                    foreach (var (kind, shown) in streamer.Push(piece)) onEvent(new AiStreamEvent(kind, shown));
                }
                if (stopped) break;
                batch.Clear();
                batch.Add(token, position++, LLamaSeqId.Zero, true);
                logitsAt = 0;
                if (await context.DecodeAsync(batch, cancel) != DecodeResult.Ok) break;
                _cached.Add(token);
            }
            foreach (var (kind, shown) in streamer.Flush()) onEvent(new AiStreamEvent(kind, shown));
            var (visible, reasoning, calls) = Gemma4Chat.Parse(output.ToString());
            var blocks = new List<AiBlock>();
            if (visible.Length > 0) blocks.Add(AiBlock.TextBlock(visible));
            blocks.AddRange(calls);
            var stop = calls.Count > 0 ? "tool_use" : made >= max ? "max_tokens" : "end";
            return new AiResult(blocks, reasoning, stop, new AiUsage(tokens.Length, made, null), model.Id);
        }
        finally { _one.Release(); }
    }
}
