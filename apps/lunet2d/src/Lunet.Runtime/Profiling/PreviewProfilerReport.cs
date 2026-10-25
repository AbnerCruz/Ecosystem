using System.Globalization;

namespace Lunet.Runtime.Profiling;

/// <summary>Relatório textual da mesma janela real exibida no Preview; não coleta métricas adicionais.</summary>
public static class PreviewProfilerReport
{
    /// <summary>Formata uma captura para o painel ao vivo, workspace e exportação, independentemente do idioma do aparelho.</summary>
    public static string Format(PreviewFrameSnapshot frame, DateTimeOffset capturedAt, bool paused,
        long managedHeapBytes, long textureBytes, long peakTextureBytes, int liveTextures,
        int gc0, int gc1, int gc2) => Format(frame, capturedAt, paused, managedHeapBytes, textureBytes,
            peakTextureBytes, liveTextures, gc0, gc1, gc2, null);

    /// <summary>Inclui somente o contador nativo das saídas de música PCM efetivamente medidas na sessão.</summary>
    public static string Format(PreviewFrameSnapshot frame, DateTimeOffset capturedAt, bool paused,
        long managedHeapBytes, long textureBytes, long peakTextureBytes, int liveTextures,
        int gc0, int gc1, int gc2, long? musicUnderruns)
    {
        if (frame.Samples == 0) return "PROFILER  |  coletando...";
        return FormattableString.Invariant($"PROFILER  |  {frame.Samples} quadros{(paused ? "  [PAUSADO]" : "")}\n") +
            "Captura: " + capturedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + "\n" +
            FormattableString.Invariant($"FPS: {frame.FramesPerSecond:F1}  Frame: {frame.FrameMilliseconds:F2} ms\n") +
            FormattableString.Invariant($"Frame P50/P95: {frame.P50FrameMilliseconds:F1}/{frame.P95FrameMilliseconds:F1} ms\n") +
            FormattableString.Invariant($"Pior: {frame.WorstFrameMilliseconds:F1} ms | engasgos: {frame.HitchFrames}\n") +
            FormattableString.Invariant($"CPU Tick: {frame.CpuTickMilliseconds:F2} ms\n") +
            (frame.PhasedSamples == 0 ? "CPU Update/Draw: não medidos\n" :
                FormattableString.Invariant($"CPU Update: {frame.UpdateCpuMilliseconds:F2} ms  ({frame.UpdateStepsPerFrame:F1} passos)\nCPU Draw: {frame.DrawCpuMilliseconds:F2} ms\n")) +
            FormattableString.Invariant($"Draw calls: {frame.DrawCallsPerFrame:F1}  Tris: {frame.TrianglesPerFrame:F0}\nAlloc GL: {frame.AllocatedBytesPerFrame:F0} B/frame\n") +
            FormattableString.Invariant($"Heap .NET: {managedHeapBytes / 1048576.0:F1} MiB\n") +
            FormattableString.Invariant($"GPU texturas RGBA8 (estim.): {textureBytes / 1048576.0:F2} MiB, {liveTextures} vivas\nPico texturas: {peakTextureBytes / 1048576.0:F2} MiB\n") +
            FormattableString.Invariant($"GC (0/1/2): {gc0}/{gc1}/{gc2}\nFrames > 33 ms: {frame.SlowFrames}/{frame.Samples}\n") +
            (musicUnderruns is null ? "GPU total/tempo e audio underruns: não medidos" :
                FormattableString.Invariant($"Audio underruns (música WAV PCM, sessão): {musicUnderruns}\n") +
                "GPU total/tempo, efeitos SoundPool e música comprimida: não medidos");
    }
}
