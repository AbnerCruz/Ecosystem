using System.Security.Cryptography;
using SharpCompress.Readers;
using SherpaOnnx;
using Urbe.Core.Voice;

namespace Urbe.Client;

/// <summary>
/// #1004 / DEC-0067 A: natural voices that run on the device. A model is downloaded once, when the person chooses one of
/// its voices; the file is checked against the size and SHA-256 of <see cref="NeuralVoices"/> before it is unpacked
/// (nothing unchecked is kept), into the app's private folder, with its licences. Speaking writes a WAV, the same as
/// the device's voice, so narration, podcasts and the Assistente's videos use it without another pipeline.
/// </summary>
public sealed class NeuralSpeech
{
    private readonly string _root;
    private readonly Func<HttpClient> _http;
    private readonly SemaphoreSlim _one = new(1, 1);
    private (string Model, OfflineTts Tts)? _loaded;

    /// <param name="root">Where models are kept (the app's private folder, not the vault).</param>
    public NeuralSpeech(string root, Func<HttpClient>? http = null)
    {
        _root = root;
        _http = http ?? (() => new HttpClient { Timeout = TimeSpan.FromMinutes(30) });
    }

    /// <summary>The one the app uses; null until a head sets a folder for it.</summary>
    public static NeuralSpeech? Shared { get; set; }

    /// <summary>
    /// Puts natural voices in front of the device's: when the chosen voice is a natural one and its model is
    /// installed, it speaks; otherwise the device's voice does, as before. In a podcast the second host takes the
    /// model's other voice (Dora and Alex), or the device's when the model has only one.
    /// </summary>
    public static void Attach(NeuralSpeech speech)
    {
        Shared = speech;
        if (_attached) return;
        _attached = true;
        var device = PlatformAudio.SynthesizeToWav;
        PlatformAudio.SynthesizeToWav = async (text, host, target, cancel) =>
        {
            if (Shared is { } neural && NeuralVoices.Find(PlatformAudio.PreferredVoice) is { } chosen && neural.IsInstalled(NeuralVoices.ModelOf(chosen)))
            {
                var voice = host == 0 ? chosen : NeuralVoices.All.FirstOrDefault(v => v.Model == chosen.Model && v != chosen);
                if (voice is not null) return await neural.SpeakAsync(voice, text, target, cancel);
            }
            return device is not null && await device(text, host, target, cancel);
        };
        var list = PlatformAudio.Voices;
        PlatformAudio.Voices = () => [.. NeuralVoices.All.Select(v => v.Id), .. list?.Invoke() ?? []];
    }

    private static bool _attached;

    public string FolderOf(NeuralVoiceModel model) => Path.Combine(_root, model.Folder);

    /// <summary>True when the model is unpacked and its marker (written last) says it was checked.</summary>
    public bool IsInstalled(NeuralVoiceModel model) => File.Exists(Path.Combine(FolderOf(model), ".verificado"));

    /// <summary>Downloads, checks and unpacks a model; progress goes from 0 to 1. Throws with what went wrong.</summary>
    public async Task InstallAsync(NeuralVoiceModel model, IProgress<double>? progress, CancellationToken cancel)
    {
        Directory.CreateDirectory(_root);
        var part = Path.Combine(_root, model.Id + ".baixando");
        try
        {
            using (var http = _http())
            using (var response = await http.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, cancel))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancel);
                await using var file = File.Create(part);
                var buffer = new byte[1 << 16];
                long done = 0;
                for (int n; (n = await source.ReadAsync(buffer, cancel)) > 0;)
                {
                    await file.WriteAsync(buffer.AsMemory(0, n), cancel);
                    done += n;
                    if (done > model.Bytes) throw new InvalidDataException("o arquivo da voz veio maior do que o esperado; nada foi instalado");
                    progress?.Report(0.9 * done / model.Bytes);
                }
            }
            if (new FileInfo(part).Length != model.Bytes) throw new InvalidDataException("o arquivo da voz veio incompleto; nada foi instalado");
            string sha;
            await using (var check = File.OpenRead(part)) sha = Convert.ToHexString(await SHA256.HashDataAsync(check, cancel)).ToLowerInvariant();
            if (sha != model.Sha256) throw new InvalidDataException("o arquivo da voz não confere com o esperado (SHA-256); nada foi instalado");
            var target = FolderOf(model);
            if (Directory.Exists(target)) Directory.Delete(target, true);
            await Task.Run(() => Unpack(part, _root, cancel), cancel);
            await File.WriteAllTextAsync(Path.Combine(target, "LICENCAS-NUMA.txt"), model.Licences, cancel);
            await File.WriteAllTextAsync(Path.Combine(target, ".verificado"), sha, cancel);
            progress?.Report(1);
        }
        finally { try { File.Delete(part); } catch (IOException) { } }
    }

    /// <summary>Unpacks a .tar.bz2 under <paramref name="root"/>; an entry that would land outside it is refused.</summary>
    public static void Unpack(string archive, string root, CancellationToken cancel = default)
    {
        var full = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        using var stream = File.OpenRead(archive);
        using var reader = ReaderFactory.OpenReader(stream);
        while (reader.MoveToNextEntry())
        {
            cancel.ThrowIfCancellationRequested();
            if (reader.Entry.IsDirectory || reader.Entry.Key is not { Length: > 0 } key) continue;
            var path = Path.GetFullPath(Path.Combine(root, key));
            if (!path.StartsWith(full, StringComparison.Ordinal)) throw new InvalidDataException("o pacote da voz tem um caminho fora da pasta: " + key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var output = File.Create(path);
            reader.WriteEntryTo(output);
        }
    }

    /// <summary>Removes a model (its voices go back to "baixar").</summary>
    public void Remove(NeuralVoiceModel model)
    {
        if (_loaded?.Model == model.Id) { _loaded.Value.Tts.Dispose(); _loaded = null; }
        if (Directory.Exists(FolderOf(model))) Directory.Delete(FolderOf(model), true);
    }

    /// <summary>Speaks <paramref name="text"/> with <paramref name="voice"/> into a WAV at <paramref name="target"/>.</summary>
    public async Task<bool> SpeakAsync(NeuralVoice voice, string text, string target, CancellationToken cancel)
    {
        var model = NeuralVoices.ModelOf(voice);
        if (!IsInstalled(model) || string.IsNullOrWhiteSpace(text)) return false;
        await _one.WaitAsync(cancel);
        try
        {
            return await Task.Run(() =>
            {
                var tts = Load(model, voice.Engine);
                var audio = tts.Generate(text, 1.0f, voice.Speaker);
                cancel.ThrowIfCancellationRequested();
                return audio.Samples.Length > 0 && audio.SaveToWaveFile(target);
            }, cancel);
        }
        finally { _one.Release(); }
    }

    // one model in memory at a time: they take hundreds of MB
    private OfflineTts Load(NeuralVoiceModel model, string engine)
    {
        if (_loaded is { } l && l.Model == model.Id) return l.Tts;
        _loaded?.Tts.Dispose();
        _loaded = null;
        var dir = FolderOf(model);
        var config = new OfflineTtsConfig();
        config.Model.NumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        if (engine == "kokoro")
        {
            config.Model.Kokoro.Model = Path.Combine(dir, "model.int8.onnx");
            config.Model.Kokoro.Voices = Path.Combine(dir, "voices.bin");
            config.Model.Kokoro.Tokens = Path.Combine(dir, "tokens.txt");
            config.Model.Kokoro.DataDir = Path.Combine(dir, "espeak-ng-data");
            config.Model.Kokoro.Lang = "pt-br";   // no English lexicon: it drops Portuguese words
        }
        else
        {
            config.Model.Vits.Model = Directory.GetFiles(dir, "*.onnx").First();
            config.Model.Vits.Tokens = Path.Combine(dir, "tokens.txt");
            config.Model.Vits.DataDir = Path.Combine(dir, "espeak-ng-data");
        }
        var tts = new OfflineTts(config);
        _loaded = (model.Id, tts);
        return tts;
    }
}
