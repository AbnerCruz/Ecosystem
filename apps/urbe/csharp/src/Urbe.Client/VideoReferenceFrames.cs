using System.Diagnostics;
using System.Globalization;
using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client;

public sealed record VideoReferenceFrame(double Seconds, byte[] Image);

public static class VideoReferenceFrames
{
    public static Func<string, CancellationToken, Task<IReadOnlyList<VideoReferenceFrame>>>? ReadNative { get; set; }

    public static double[] SampleTimes(double duration) => duration > 0 && double.IsFinite(duration)
        ? [duration * 0.05, duration * 0.2, duration * 0.4, duration * 0.6, duration * 0.8, duration * 0.95]
        : [0, 1, 2, 4, 8, 12];

    public static async Task<IReadOnlyList<VideoReferenceFrame>> ReadAsync(string source, CancellationToken cancel)
    {
        if (ReadNative is { } native) return await native(source, cancel);
        if (OperatingSystem.IsAndroid() || FfmpegVideoEncoder.Find() is not { } ffmpeg) return [];
        var folder = Directory.CreateTempSubdirectory("numa-reference-frames-");
        try
        {
            double duration = 0;
            try { duration = Mp4Inspection.Read(source)?.Seconds ?? 0; }
            catch (InvalidDataException) { }
            var frames = new List<VideoReferenceFrame>();
            foreach (var at in SampleTimes(duration))
            {
                var target = Path.Combine(folder.FullName, "frame.jpg");
                var info = new ProcessStartInfo(ffmpeg)
                {
                    RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
                };
                foreach (var arg in new[] { "-nostdin", "-y", "-v", "error", "-protocol_whitelist", "file",
                    "-format_whitelist", "mov,matroska,webm,avi,mpeg,mpegts,flv,ogg,asf",
                    "-ss", at.ToString("0.###", CultureInfo.InvariantCulture), "-i", source, "-map", "0:v:0",
                    "-frames:v", "1", "-vf", "scale=480:480:force_original_aspect_ratio=decrease", target })
                    info.ArgumentList.Add(arg);
                using var process = Process.Start(info) ?? throw new IOException("ffmpeg não iniciou.");
                _ = process.StandardError.ReadToEndAsync();
                try { await process.WaitForExitAsync(cancel); }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch (InvalidOperationException) { }
                    throw;
                }
                if (process.ExitCode == 0 && File.Exists(target))
                    frames.Add(new(at, await File.ReadAllBytesAsync(target, cancel)));
                File.Delete(target);
            }
            return frames;
        }
        finally
        {
            try { folder.Delete(true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    public static byte[]? ContactSheet(IReadOnlyList<VideoReferenceFrame> frames)
    {
        if (frames.Count == 0) return null;
        const int width = 320, height = 224;
        using var sheet = new SKBitmap(width * 2, height * ((frames.Count + 1) / 2));
        using var canvas = new SKCanvas(sheet);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 15);
        canvas.Clear(SKColors.Black);
        for (int i = 0; i < frames.Count; i++)
        {
            using var image = SKBitmap.Decode(frames[i].Image);
            if (image is null) continue;
            var scale = Math.Min(width / (float)image.Width, (height - 24) / (float)image.Height);
            float x = width * (i % 2), y = height * (i / 2);
            canvas.DrawBitmap(image, SKRect.Create(x, y, image.Width * scale, image.Height * scale));
            canvas.DrawText(TimeSpan.FromSeconds(frames[i].Seconds).ToString(@"mm\:ss\.f"), x + 6, y + height - 5,
                SKTextAlign.Left, font, paint);
        }
        using var encoded = sheet.Encode(SKEncodedImageFormat.Jpeg, 82);
        return encoded?.ToArray();
    }
}
