using Avalonia.Controls;

namespace Lunet.Client.Shell;

/// <summary>
/// The game running on the GPU inside the Jogo tab (M5), offered by a head that has one (Android: the GLES Preview of
/// the previous screens, hosted in the shell). The portable CPU host stays the path on heads without it (desktop,
/// tests). The view takes the touches itself; pause, one-frame step, restart and stop come from the shell's bar.
/// </summary>
public interface IGpuGamePreview : IDisposable
{
    /// <summary>The native surface, already wrapped as an Avalonia control.</summary>
    Control View { get; }
    /// <summary>Frames the game really drew on the GPU so far.</summary>
    long FramesRendered { get; }
    /// <summary>Name shown in the status ("GPU · OpenGL ES 3").</summary>
    string Backend { get; }
    void SetPaused(bool paused);
    void Step();
    void Restart();
    /// <summary>The Preview's own Inspector over the GPU game (it reads the live game objects).</summary>
    void ToggleInspector();
    /// <summary>The Preview's own profiler: real frame times, draw calls and texture memory measured by the GLES backend.</summary>
    void ToggleProfiler();
    /// <summary>A key of a physical keyboard while the game has the focus.</summary>
    void Key(Lunet.Input.Keys key, bool down);
    /// <summary>The app left or came back to the screen (GL surface pause/resume).</summary>
    void SetAppVisible(bool visible);
}

/// <summary>
/// Creates the GPU preview for a compiled game. <c>fault</c> must be called (on any thread) when the game dies; the
/// session posts it to the UI thread.
/// </summary>
public delegate IGpuGamePreview GpuPreviewFactory(Lunet.Compiler.CompileResult compiled, Lunet.Core.LunetProject project, Action<string> fault);
