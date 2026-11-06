using System.Diagnostics;
using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-209: what the device offers to make a video. A head sets <see cref="CreateEncoder"/> (Android: MediaCodec +
/// MediaMuxer); on a computer the Urbe uses an ffmpeg it finds. Null means no MP4 here, and the studio says why.
/// </summary>
public static class PlatformVideo
{
    public static Func<IVideoEncoder>? CreateEncoder { get; set; }

    /// <summary>Decodes a sound file (any format the platform reads) into a 16-bit PCM WAV; false when it cannot.</summary>
    public static Func<string, string, CancellationToken, Task<bool>>? DecodeAudio { get; set; }

    /// <summary>Why no encoder is available, for the person.</summary>
    public static string Unavailable => OperatingSystem.IsAndroid()
        ? "Este aparelho não oferece o codificador de vídeo H.264."
        : "Para exportar MP4 no computador, instale o ffmpeg (ffmpeg.org) e deixe-o no PATH; o Urbe o encontra sozinho.";

    /// <summary>The head's encoder, else ffmpeg on a computer, else null.</summary>
    public static IVideoEncoder? Encoder() =>
        CreateEncoder?.Invoke() ?? (OperatingSystem.IsAndroid() ? null : FfmpegVideoEncoder.Find() is { } path ? new FfmpegVideoEncoder(path) : null);

    public static bool CanDecode => DecodeAudio is not null || PlatformAudio.DecodeToWav is not null || !OperatingSystem.IsAndroid() && FfmpegVideoEncoder.Find() is not null;

    /// <summary>A sound file as PCM16 WAV: a WAV as it is, otherwise through the platform's decoder.</summary>
    public static async Task<bool> ToWavAsync(string source, string target, CancellationToken cancel)
    {
        await using (var probe = File.OpenRead(source))
            if (VideoAudio.ReadWavInfo(probe) is not null) { probe.Close(); File.Copy(source, target, true); return true; }
        if (DecodeAudio is { } decode) return await decode(source, target, cancel);
        if (!OperatingSystem.IsAndroid() && FfmpegVideoEncoder.Find() is { } ffmpeg) return await FfmpegVideoEncoder.DecodeAsync(ffmpeg, source, target, cancel);
        if (PlatformAudio.DecodeToWav is { } fallback) return await fallback(source, target, null, cancel);
        return false;
    }
}

public sealed record VideoExportProgress(int Frame, int FrameCount, string Phase)
{
    public double Fraction => FrameCount == 0 ? 0 : Frame / (double)FrameCount;
}

public sealed record VideoExportResult(string Path, Mp4Info Info);

/// <summary>
/// Renders every frame of the timeline and hands them to the encoder, then checks the MP4 it wrote (H.264, size,
/// frame count, sound). The file only appears at <c>outputPath</c> when the check passed; cancelled or failed, the
/// partial file is deleted and nothing is reported as saved.
/// </summary>
public static class VideoExport
{
    public static async Task<VideoExportResult> RunAsync(VideoFrameRenderer renderer, IVideoEncoder encoder, string outputPath,
        string? audioWavPath = null, IProgress<VideoExportProgress>? progress = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(encoder);
        var timeline = renderer.Timeline;
        if (timeline.FrameCount == 0) throw new InvalidOperationException("O vídeo não tem cenas.");
        var settings = new VideoEncodeSettings(timeline.Width, timeline.Height, timeline.Fps, timeline.FrameCount, audioWavPath);
        var partial = outputPath + ".part.mp4";
        TryDelete(partial);
        try
        {
            await encoder.StartAsync(settings, partial, cancel);
            await Task.Run(async () =>
            {
                using var bitmap = new SKBitmap(new SKImageInfo(timeline.Width, timeline.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                var buffer = new byte[timeline.Width * timeline.Height * 4];
                for (int i = 0; i < timeline.FrameCount; i++)
                {
                    cancel.ThrowIfCancellationRequested();
                    renderer.RenderInto(timeline.FrameTime(i), bitmap);
                    System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), buffer, 0, buffer.Length);
                    await encoder.WriteFrameAsync(buffer, cancel);
                    progress?.Report(new VideoExportProgress(i + 1, timeline.FrameCount, "Desenhando e codificando"));
                }
            }, cancel);
            progress?.Report(new VideoExportProgress(timeline.FrameCount, timeline.FrameCount, "Finalizando o MP4"));
            await encoder.FinishAsync(cancel);
            await encoder.DisposeAsync();
            var info = Mp4Inspection.Read(partial);
            if (Mp4Inspection.Problem(info, settings) is { } problem)
                throw new InvalidDataException("O MP4 gerado não passou na verificação: " + problem + ".");
            File.Move(partial, outputPath, true);
            progress?.Report(new VideoExportProgress(timeline.FrameCount, timeline.FrameCount, "Verificado"));
            return new VideoExportResult(outputPath, info!);
        }
        catch
        {
            try { await encoder.DisposeAsync(); } catch (Exception e) when (e is IOException or InvalidOperationException or ObjectDisposedException) { }
            TryDelete(partial);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>
/// The desktop adapter: an ffmpeg found on the computer encodes H.264 (yuv420p, playable everywhere) and AAC from
/// raw frames on its standard input. Nothing is bundled; without ffmpeg the studio explains how to get it.
/// </summary>
public sealed class FfmpegVideoEncoder(string ffmpeg) : IVideoEncoder
{
    private Process? _process;
    private Stream? _input;
    private Task<string>? _errors;
    private VideoEncodeSettings? _settings;
    private int _frames;

    public string Name => "ffmpeg";

    /// <summary>The ffmpeg executable on the PATH (or URBE_FFMPEG), or null.</summary>
    public static string? Find()
    {
        if (Environment.GetEnvironmentVariable("URBE_FFMPEG") is { Length: > 0 } given && File.Exists(given)) return given;
        var names = OperatingSystem.IsWindows() ? new[] { "ffmpeg.exe" } : ["ffmpeg"];
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var name in names)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    public static async Task<bool> DecodeAsync(string ffmpeg, string source, string target, CancellationToken cancel)
    {
        var info = new ProcessStartInfo(ffmpeg) { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in new[] { "-nostdin", "-y", "-v", "error", "-i", source, "-vn", "-ac", "2", "-ar", VideoAudio.Rate.ToString(), "-c:a", "pcm_s16le", "-f", "wav", target })
            info.ArgumentList.Add(a);
        using var process = Process.Start(info) ?? throw new IOException("ffmpeg não iniciou.");
        _ = process.StandardOutput.ReadToEndAsync(cancel);
        _ = process.StandardError.ReadToEndAsync(cancel);
        try { await process.WaitForExitAsync(cancel); }
        catch (OperationCanceledException) { try { process.Kill(true); } catch (InvalidOperationException) { } throw; }
        return process.ExitCode == 0 && File.Exists(target);
    }

    private static string? H264Encoder(string ffmpeg)
    {
        try
        {
            var info = new ProcessStartInfo(ffmpeg) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in new[] { "-hide_banner", "-encoders" }) info.ArgumentList.Add(a);
            using var p = Process.Start(info)!;
            _ = p.StandardError.ReadToEndAsync();
            var list = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return new[] { "libx264", "libopenh264", "h264_mf", "h264_videotoolbox", "h264_nvenc", "h264_qsv", "h264_amf", "h264_vaapi" }
                .FirstOrDefault(e => list.Contains(" " + e + " ", StringComparison.Ordinal));
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or InvalidOperationException) { return null; }
    }

    public Task StartAsync(VideoEncodeSettings settings, string outputPath, CancellationToken cancel)
    {
        if (_process is not null) throw new InvalidOperationException("O codificador já começou.");
        var codec = H264Encoder(ffmpeg) ?? throw new NotSupportedException("O ffmpeg deste computador não tem codificador H.264.");
        _settings = settings;
        var info = new ProcessStartInfo(ffmpeg) { RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        var args = new List<string> { "-hide_banner", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgba", "-s", settings.Width + "x" + settings.Height,
            "-r", settings.Fps.ToString(), "-i", "pipe:0" };
        if (settings.AudioWavPath is { } wav) args.AddRange(["-i", wav]);
        args.AddRange(["-map", "0:v:0"]);
        if (settings.AudioWavPath is not null) args.AddRange(["-map", "1:a:0", "-c:a", "aac", "-b:a", "160k"]);
        args.AddRange(["-c:v", codec, "-pix_fmt", "yuv420p", "-r", settings.Fps.ToString(), "-frames:v", settings.FrameCount.ToString()]);
        if (codec == "libx264") args.AddRange(["-preset", "veryfast", "-crf", "20", "-bf", "0"]);
        if (settings.AudioWavPath is not null) args.AddRange(["-t", (settings.FrameCount / (double)settings.Fps).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)]);
        args.AddRange(["-movflags", "+faststart", "-f", "mp4", outputPath]);
        foreach (var a in args) info.ArgumentList.Add(a);
        _process = Process.Start(info) ?? throw new IOException("ffmpeg não iniciou.");
        _input = _process.StandardInput.BaseStream;
        _errors = _process.StandardError.ReadToEndAsync(CancellationToken.None);
        _ = _process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task WriteFrameAsync(ReadOnlyMemory<byte> rgba, CancellationToken cancel)
    {
        if (_input is null || _settings is null) throw new InvalidOperationException("O codificador não começou.");
        if (rgba.Length != _settings.Width * _settings.Height * 4) throw new ArgumentException("Quadro com tamanho errado.", nameof(rgba));
        try { await _input.WriteAsync(rgba, cancel); }
        catch (IOException e) { throw new IOException("O ffmpeg parou: " + await Errors(), e); }
        _frames++;
    }

    public async Task FinishAsync(CancellationToken cancel)
    {
        if (_process is null || _input is null) throw new InvalidOperationException("O codificador não começou.");
        await _input.FlushAsync(cancel);
        _input.Close();
        _input = null;
        await _process.WaitForExitAsync(cancel);
        if (_process.ExitCode != 0) throw new IOException("O ffmpeg falhou: " + await Errors());
    }

    private async Task<string> Errors() => _errors is null ? "" : (await _errors).Trim();

    public async ValueTask DisposeAsync()
    {
        try { _input?.Close(); } catch (IOException) { }
        _input = null;
        if (_process is { } p)
        {
            try { if (!p.HasExited) { p.Kill(true); await p.WaitForExitAsync(); } }
            catch (InvalidOperationException) { }
            p.Dispose();
            _process = null;
        }
    }
}
