using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>One app-private reading-state writer, shared by the modal, pinned and split readers.</summary>
public sealed class ReadingStateFile
{
    private readonly string? _path;
    private ReaderSessionCatalog _catalog;
    private readonly SemaphoreSlim _writer = new(1, 1);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private long _generation;
    public bool HasPending { get; private set; }
    public string? Error { get; private set; }
    public bool ReadOnly => _catalog.ReadOnly;

    public ReadingStateFile(string? path)
    {
        _path = path;
        try { _catalog = ReaderSessionCatalog.Read(path is not null && File.Exists(path) ? File.ReadAllBytes(path) : null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _catalog = ReaderSessionCatalog.Read("invalid"u8.ToArray()); }
        Error = _catalog.Error;
        _timer.Tick += async (_, _) => { _timer.Stop(); await FlushAsync(); };
    }
    public ReaderSession? Restore(string vault, string path, string revision) => _catalog.Restore(vault, path, revision);
    public ReaderSession? Latest(string vault, string path) => _catalog.Latest(vault, path);
    public IReadOnlyList<ReaderSession> Recent(string vault, int max) => _catalog.Recent(vault, max);
    public ReaderPreferences PreferencesFor(string format) => _catalog.PreferencesFor(format);
    public ReaderPreferences? DefaultFor(string reader) => _catalog.DefaultFor(reader);
    public void Remember(ReaderSession session)
    {
        if (!_catalog.Record(session)) return;
        Schedule();
    }
    /// <summary>The look last chosen in one kind of reader (<see cref="ReaderKinds"/>), for the next new source.</summary>
    public void RememberDefault(string reader, ReaderPreferences preferences)
    {
        if (!_catalog.RecordDefault(new(reader, preferences, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))) return;
        Schedule();
    }
    private void Schedule()
    {
        _generation++; HasPending = true;
        _timer.Stop(); _timer.Start();
    }
    public void Move(string vault, string from, string to, bool folder = false)
    {
        _catalog.Move(vault, from, to, folder);
        if (ReadOnly) return;
        _generation++; HasPending = true;
        _timer.Stop(); _timer.Start();
    }
    public async Task<bool> FlushAsync()
    {
        if (_path is null) { HasPending = false; return true; }
        if (ReadOnly) return false;
        await _writer.WaitAsync();
        try
        {
            while (HasPending)
            {
                long generation = _generation;
                byte[] snapshot = _catalog.Serialize();
                var merged = await Task.Run(async () =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
                    using var exclusive = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    var disk = ReaderSessionCatalog.Read(File.Exists(_path) ? await File.ReadAllBytesAsync(_path) : null);
                    if (disk.ReadOnly) return disk;
                    disk.Merge(ReaderSessionCatalog.Read(snapshot));
                    var bytes = disk.Serialize();
                    var temp = _path + ".tmp-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        await File.WriteAllBytesAsync(temp, bytes);
                        if (!(await File.ReadAllBytesAsync(temp)).AsSpan().SequenceEqual(bytes)) throw new IOException("Posição não verificada.");
                        File.Move(temp, _path, true);
                        if (!(await File.ReadAllBytesAsync(_path)).AsSpan().SequenceEqual(bytes)) throw new IOException("Posição não verificada após gravação.");
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                    return disk;
                });
                if (merged.ReadOnly) { _catalog = merged; Error = merged.Error; HasPending = false; return false; }
                _catalog.Merge(merged, preferIncomingTies:false);
                HasPending = generation != _generation;
                Error = null;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { Error = "Não foi possível salvar a posição de leitura. O arquivo anterior foi preservado."; return false; }
        finally { _writer.Release(); }
    }
}
