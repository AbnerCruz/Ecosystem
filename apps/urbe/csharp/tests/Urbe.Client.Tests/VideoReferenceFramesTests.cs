using System.Diagnostics;
using SkiaSharp;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed class VideoReferenceFramesTests
{
    [Fact]
    public async Task ReadsFramesAcrossAnActualMp4AndBuildsAnImage()
    {
        if (FfmpegVideoEncoder.Find() is not { } ffmpeg) return;
        var directory = Directory.CreateTempSubdirectory("numa-reference-test-");
        try
        {
            var source = Path.Combine(directory.FullName, "source.mp4");
            var info = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, RedirectStandardError = true };
            foreach (var argument in new[] { "-nostdin", "-v", "error", "-f", "lavfi", "-i",
                "testsrc2=size=160x90:rate=10:duration=4", "-c:v", "mpeg4", source })
                info.ArgumentList.Add(argument);
            using var process = Process.Start(info)!;
            var errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancel.CancelAfter(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(cancel.Token);
            Assert.True(process.ExitCode == 0, await errors);
            var frames = await VideoReferenceFrames.ReadAsync(source, cancel.Token);
            Assert.Equal(6, frames.Count);
            Assert.InRange(frames[^1].Seconds, 3.5, 4);
            using var sheet = SKBitmap.Decode(VideoReferenceFrames.ContactSheet(frames)!);
            Assert.NotNull(sheet);
            Assert.Equal((640, 672), (sheet.Width, sheet.Height));
        }
        finally { directory.Delete(true); }
    }
}
