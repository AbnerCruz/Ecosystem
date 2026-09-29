namespace Lunet.Audio;

/// <summary>Música longa tocada em streaming (uma por vez). Crie via <c>Content.LoadMusic</c> e toque com <c>Audio.PlayMusic</c>.</summary>
public sealed class Music : IDisposable
{
    private readonly IAudioBackend _backend;

    internal Music(IAudioBackend backend, int id, string name)
    {
        _backend = backend;
        Id = id;
        Name = name;
    }

    public string Name { get; }
    public bool IsDisposed { get; private set; }
    internal int Id { get; }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _backend.UnloadMusic(Id);
    }
}
