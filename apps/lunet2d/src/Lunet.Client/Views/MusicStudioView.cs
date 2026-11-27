using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Text.Json;
using Lunet.Client.Ui;
using Lunet.Core;
using MusicNote = Lunet.Studio.Music.MusicNote;
using MusicPattern = Lunet.Studio.Music.MusicPattern;
using MusicRenderer = Lunet.Studio.Music.MusicRenderer;
using MusicSong = Lunet.Studio.Music.MusicSong;
using MusicTheory = Lunet.Studio.Music.MusicTheory;
using MusicTrack = Lunet.Studio.Music.MusicTrack;
using Instrument = Lunet.Studio.Music.Instrument;

namespace Lunet.Client.Views;

/// <summary>What the bottom of the Music Studio shows: the notes, the selected track's sound, or the song's order.</summary>
public enum MusicPanel { Notes, Track, Arrangement }

/// <summary>
/// The piano roll: rows are the notes of two octaves from the song's root (scale notes lit, the root marked), columns
/// are the steps of the pattern (beats marked). Notes of the selected track are bright; other tracks show faintly.
/// </summary>
public sealed class PianoRoll : ZoomCanvas
{
    public const int Octaves = 2;
    private MusicSong _song = MusicSong.Empty("");
    private int _pattern, _track;

    public PianoRoll() => AutomationProperties.SetName(this, "Piano roll");

    protected override (double Width, double Height) ContentSize => (Steps * 24, Rows * 18);
    protected override (int X, int Y) Cells => (Steps, Rows);

    public int Rows => Octaves * 12;
    public int Steps => _song.Patterns.Count == 0 ? 16 : _song.Patterns[_pattern].Steps;

    /// <summary>The pitch of a row (row 0 is the highest note).</summary>
    public int LowestPitch { get; internal set; } = 60;
    public int PitchOfRow(int row) => LowestPitch + Rows - 1 - row;

    public void Show(MusicSong song, int pattern, int track)
    {
        _song = song; _pattern = pattern; _track = track;
        InvalidateVisual();
    }

    protected override void RenderContent(DrawingContext context, Rect a)
    {
        if (a.Width <= 0 || _song.Patterns.Count == 0) return;
        var pattern = _song.Patterns[_pattern];
        var cw = a.Width / Steps; var rh = a.Height / Rows;
        var scale = MusicTheory.Scale(_song.Scale).Intervals;
        for (var row = 0; row < Rows; row++)
        {
            var pitch = PitchOfRow(row);
            var degree = ((pitch - _song.Root) % 12 + 12) % 12;
            var color = degree == 0 ? "#2A2448" : scale.Contains(degree) ? LunetTokens.Ink.I3 : LunetTokens.Ink.I2;
            context.FillRectangle(LunetTokens.Brush(color), new Rect(a.X, a.Y + row * rh, a.Width, rh));
        }
        var beat = Math.Max(1, _song.StepsPerBeat);
        var thin = new Pen(LunetTokens.Brush("#14ffffff"), 1);
        var thick = new Pen(LunetTokens.Brush("#33ffffff"), 1);
        for (var s = 1; s < Steps; s++)
        {
            var x = Math.Round(a.X + s * cw) + .5;
            context.DrawLine(s % (beat * 4) == 0 ? thick : thin, new Point(x, a.Y), new Point(x, a.Bottom));
        }
        foreach (var note in pattern.Notes)
        {
            var row = Rows - 1 - (note.Pitch - LowestPitch);
            if (row < 0 || row >= Rows) continue;
            var mine = note.Track == _track;
            var rect = new Rect(a.X + note.Step * cw + 1, a.Y + row * rh + 1, Math.Max(2, note.Length * cw - 2), Math.Max(2, rh - 2));
            context.FillRectangle(LunetTokens.Brush(mine ? LunetTokens.Color.Primary : "#59A594FF"), rect, 3);
        }
        context.DrawRectangle(new Pen(LunetTokens.Brush(LunetTokens.Color.BorderStrong), 1), a);
    }
}

/// <summary>
/// The Music Studio in the shell (A3, first slice): a piano roll per pattern and track, the song's tracks and
/// patterns as chips, tempo, and play — all at the bottom. A new song starts from the core's Starter (a progression,
/// a melody, bass and drums), so there is something to hear and change at once. Voltar saves the editable
/// <c>.song.json</c> and its <c>.wav</c> together, as the previous Studio does.
/// </summary>
public sealed class MusicStudioView : UserControl
{
    private readonly LunetShell _shell;
    private readonly string _path;
    private readonly Action _close;
    private readonly StudioAssetWorkspace _storage;
    private readonly PianoRoll _roll = new();
    private readonly StackPanel _tracks = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly StackPanel _patterns = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly TextBlock _info = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly Slider _bpm = new() { Minimum = 30, Maximum = 300, MinHeight = 40, Name = "Bpm" };
    private readonly StackPanel _panelTabs = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly StackPanel _panel = new() { Spacing = 0 };
    private readonly DockPanel _bpmRow = new() { Margin = new Thickness(LunetTokens.Space.M, 0) };
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly ScrollViewer _tracksRow, _patternsRow;
    private CancellationTokenSource? _previewCancellation;
    private Task _preview = Task.CompletedTask;
    private bool _saving;
    private bool _dirty;
    private bool? _adding;
    private int? _orderSelected;

    public MusicStudioView(LunetShell shell, string path, Action close, StudioAssetWorkspace? storage = null)
    {
        _shell = shell;
        _path = path;
        _close = close;
        // A game's folder, or the standalone Studio folder (Home → Studios) when given.
        _storage = storage ?? new StudioAssetWorkspace(shell.Workspace.Project!.Directory);
        _tracksRow = Scroll(_tracks);
        _patternsRow = Scroll(_patterns);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var name = Path.GetFileName(path).Replace(".song.json", "", StringComparison.OrdinalIgnoreCase);
        try
        {
            Song = MusicSong.Parse(_storage.ReadText(path));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            Song = MusicSong.Starter(name);
            _dirty = true;
        }
        catch (Exception e) when (IsAudioError(e))
        {
            Song = MusicSong.Empty(name);
            Content = new StackPanel
            {
                Margin = new Thickness(LunetTokens.Space.M),
                Spacing = LunetTokens.Space.S,
                Children =
                {
                    LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Close()),
                    LunetUi.Secondary("Não foi possível abrir " + Path.GetFileName(path) + ": " + e.Message),
                },
            };
            return;
        }
        Pattern = Song.Patterns.Count > 1 ? 1 : 0; // the Tema of a starter song has every track
        FitTrackPitch();
        _roll.Paint += (step, row, first) => Toggle(step, row, first);
        _roll.StrokeEnded += () => { _adding = null; Refresh(); };
        _roll.StrokeCancelled += () => _adding = null;

        var back = LunetUi.IconButton(LunetIcons.Back, "Salvar e voltar", () => Close());
        var title = LunetUi.Title(Song.Name);
        var fit = LunetUi.IconButton(LunetIcons.Fit, "Ver o padrão inteiro (de novo: notas grandes)", () => { if (_roll.Zoom > 1.01) _roll.ResetView(); else _roll.FitHeight(); });
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xs, 0) };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(fit, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(fit);
        bar.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, _info } });

        AutomationProperties.SetName(_bpm, "Andamento (BPM)");
        _bpm.Value = Song.Bpm;
        _bpm.ValueChanged += (_, e) =>
        {
            Song.Bpm = (float)Math.Round(e.NewValue);
            _dirty = true;
            UpdateInfo();
        };
        var bpmLabel = LunetUi.Text("Andamento", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
        bpmLabel.Width = 84; bpmLabel.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(bpmLabel, Dock.Left);
        _bpmRow.Children.Add(bpmLabel);
        _bpmRow.Children.Add(_bpm);

        var playPattern = LunetUi.Button("Padrão", ButtonKind.Secondary, LunetIcons.Play, () => _ = PlayPattern());
        playPattern.Name = "PlayPattern";
        var playSong = LunetUi.Button("Música", ButtonKind.Primary, LunetIcons.Play, () => _ = PlaySong());
        playSong.Name = "PlaySong";
        var stop = LunetUi.Button("Parar", ButtonKind.Secondary, null, StopPreview);
        stop.Name = "StopMusic";
        var plays = new Avalonia.Controls.Primitives.UniformGrid
        {
            Columns = 3, Margin = new Thickness(LunetTokens.Space.S, 0, LunetTokens.Space.S, LunetTokens.Space.S),
            Children = { playPattern, playSong, stop },
        };
        foreach (var b in plays.Children.OfType<Button>()) { b.Margin = new Thickness(LunetTokens.Space.Xxs); b.HorizontalAlignment = HorizontalAlignment.Stretch; b.MinHeight = 52; }
        _status.Margin = new Thickness(LunetTokens.Space.M, 0);
        _status.TextWrapping = TextWrapping.Wrap;
        _status.IsVisible = false;

        var tabs = new Border { Child = _panelTabs, Padding = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs, LunetTokens.Space.S, 0) };
        var transport = new StackPanel { Children = { _status, plays } };
        DockPanel.SetDock(tabs, Dock.Top);
        DockPanel.SetDock(transport, Dock.Bottom);
        var bottom = new DockPanel
        {
            Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            Children = { tabs, transport, new ScrollViewer { Content = _panel } },
        };
        SizeChanged += (_, e) => bottom.MaxHeight = e.NewSize.Height * 0.6;
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(bottom);
        root.Children.Add(new Border { Child = _roll, Margin = new Thickness(LunetTokens.Space.S) });
        Content = root;
        // On a phone a pattern is wider than tall: start with the rows filling the height, from the first step.
        _roll.SizeChanged += (_, e) => { if (e.PreviousSize.Height <= 0 && e.NewSize.Height > 0) _roll.FitHeight(); };
        Refresh();
    }

    public MusicSong Song { get; }
    public MusicPanel Panel { get; private set; } = MusicPanel.Notes;
    /// <summary>Length in steps of the notes a tap adds (1 = a sixteenth in 4/4).</summary>
    public int NoteLength { get; private set; } = 1;
    public int Pattern { get; private set; }
    public int Track { get; private set; }
    public PianoRoll Roll => _roll;

    /// <summary>Adds or removes the note at a step and row of the piano roll (one-step notes); a drag keeps the first choice.</summary>
    public void Toggle(int step, int row, bool first)
    {
        if (_saving) return;
        var pitch = _roll.PitchOfRow(row);
        var notes = Song.Patterns[Pattern].Notes;
        var existing = notes.FindIndex(n => n.Track == Track && n.Pitch == pitch && step >= n.Step && step < n.Step + n.Length);
        if (first) _adding = existing < 0;
        if (_adding == true && existing < 0)
        {
            notes.Add(new MusicNote(Track, step, pitch, Math.Max(1, Math.Min(NoteLength, Song.Patterns[Pattern].Steps - step))));
            if (first) PreviewNote(pitch);
        }
        else if (_adding == false && existing >= 0) notes.RemoveAt(existing);
        else return;
        _dirty = true;
        _roll.Show(Song, Pattern, Track);
    }

    private void PreviewNote(int pitch)
    {
        if (_shell.PlaySound is not { } play || _previewCancellation is not null || _shell.IsSoundPlaying?.Invoke() == true) return;
        var probe = new MusicSong
        {
            Name = "nota", Bpm = Song.Bpm, StepsPerBeat = Song.StepsPerBeat, Root = Song.Root, Scale = Song.Scale,
            Tracks = [Song.Tracks[Track] with { Muted = false, Solo = false, Echo = 0, Reverb = 0 }],
            Patterns = [new MusicPattern { Name = "n", Steps = 2, Notes = [new MusicNote(0, 0, pitch, 1)] }],
            Order = [0], Loop = false,
        };
        try { play(new MusicRenderer(22_050).RenderWav(probe)); }
        catch (Exception e) when (IsAudioError(e)) { Say("Não foi possível ouvir a nota: " + e.Message, error: true); }
    }

    public void SelectTrack(int track)
    {
        Track = Math.Clamp(track, 0, Song.Tracks.Count - 1);
        FitTrackPitch();
        Refresh();
    }

    public void SelectPattern(int pattern)
    {
        Pattern = Math.Clamp(pattern, 0, Song.Patterns.Count - 1);
        FitTrackPitch();
        Refresh();
        _roll.FitHeight();
    }

    private void FitTrackPitch()
    {
        var track = Song.Tracks[Track];
        int lowest = Song.Patterns[Pattern].Notes.Where(n => n.Track == Track)
            .Select(n => n.Pitch).DefaultIfEmpty(track.Instrument.Drums ? 36 : Song.Root).Min();
        _roll.LowestPitch = Math.Clamp(lowest, 0, 128 - _roll.Rows);
    }

    private void ShiftOctave(int change)
    {
        _roll.LowestPitch = Math.Clamp(_roll.LowestPitch + change, 0, 128 - _roll.Rows);
        Refresh();
    }

    public void AddPattern(bool duplicate)
    {
        if (Song.Patterns.Count >= MusicSong.MaxPatterns) return;
        var source = Song.Patterns[Pattern];
        var name = ((char)('A' + Song.Patterns.Count % 26)).ToString();
        var pattern = new MusicPattern { Name = name, Steps = source.Steps, Notes = duplicate ? source.Notes.ToList() : [] };
        Song.Patterns.Add(pattern);
        if (Song.Order.Count < MusicSong.MaxOrder) Song.Order.Add(Song.Patterns.Count - 1);
        Pattern = Song.Patterns.Count - 1;
        _dirty = true;
        Refresh();
    }

    public void ShowPanel(MusicPanel panel) { Panel = panel; Refresh(); }
    public void SetNoteLength(int steps) { NoteLength = Math.Clamp(steps, 1, 16); Refresh(); }

    // ---------- Track (instrument and mixer) ----------

    private void ChangeTrack(Func<MusicTrack, MusicTrack> change, bool refresh = true)
    {
        Song.Tracks[Track] = change(Song.Tracks[Track]);
        _dirty = true;
        if (refresh) Refresh();
    }

    public void SetInstrument(string preset) => ChangeTrack(t => t with { Instrument = Instrument.Preset(preset) });
    public void SetVolume(float volume) => ChangeTrack(t => t with { Volume = Math.Clamp(volume, 0, 1) }, refresh: false);
    public void SetPan(float pan) => ChangeTrack(t => t with { Pan = Math.Clamp(pan, -1, 1) }, refresh: false);
    public void ToggleMute() => ChangeTrack(t => t with { Muted = !t.Muted });
    public void ToggleSolo() => ChangeTrack(t => t with { Solo = !t.Solo });

    public bool AddTrack()
    {
        if (Song.Tracks.Count >= MusicSong.MaxTracks) return false;
        Song.Tracks.Add(new MusicTrack { Name = "Faixa " + (Song.Tracks.Count + 1), Instrument = Instrument.Presets[0] });
        Track = Song.Tracks.Count - 1;
        FitTrackPitch();
        _dirty = true;
        Panel = MusicPanel.Track;
        Refresh();
        return true;
    }

    // ---------- Arrangement (the order the patterns play in) ----------

    public void AppendToOrder(int pattern)
    {
        if (Song.Order.Count >= MusicSong.MaxOrder || pattern < 0 || pattern >= Song.Patterns.Count) return;
        Song.Order.Add(pattern);
        _orderSelected = Song.Order.Count - 1;
        _dirty = true;
        Refresh();
    }

    public void MoveOrder(int index, int direction)
    {
        var target = index + direction;
        if (index < 0 || index >= Song.Order.Count || target < 0 || target >= Song.Order.Count) return;
        (Song.Order[index], Song.Order[target]) = (Song.Order[target], Song.Order[index]);
        _orderSelected = target;
        _dirty = true;
        Refresh();
    }

    public void RemoveOrder(int index)
    {
        if (index < 0 || index >= Song.Order.Count) return;
        Song.Order.RemoveAt(index);
        _orderSelected = null;
        _dirty = true;
        Refresh();
    }

    public Task PlayPattern() => PlayPreview(Pattern);
    public Task PlaySong() => PlayPreview(Song.Order.Count == 0 ? Pattern : null);

    private Task PlayPreview(int? pattern)
    {
        if (_saving) return Task.CompletedTask;
        if (_shell.PlaySound is not { } play)
        {
            Say("A prévia de áudio não está disponível nesta plataforma.", error: true);
            return Task.CompletedTask;
        }
        var previous = _preview;
        StopPreview();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        Say("Preparando o som…");
        return _preview = RenderPreview(previous, play, Song.ToJson(), pattern, cancellation);
    }

    private async Task RenderPreview(Task previous, Action<byte[]> play, string snapshot, int? pattern, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        try
        {
            await previous;
            var wav = await Task.Run(() => new MusicRenderer(32_000).RenderWav(MusicSong.Parse(snapshot), pattern, token), token);
            if (!token.IsCancellationRequested)
            {
                play(wav);
                Say("");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception e) when (IsAudioError(e))
        {
            if (!token.IsCancellationRequested) Say("Não foi possível tocar: " + e.Message, error: true);
        }
        finally
        {
            if (ReferenceEquals(_previewCancellation, cancellation)) _previewCancellation = null;
            cancellation.Dispose();
        }
    }

    public void StopPreview()
    {
        _previewCancellation?.Cancel();
        _shell.StopSound?.Invoke();
        if (!_saving) Say("");
    }

    /// <summary>
    /// UX07: left by any route (another destination, Home, the app going away), a preview still rendering stops and
    /// plays nothing over the next screen. Unsaved edits are kept by the shell's save path, not here.
    /// </summary>
    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (!_saving) StopPreview();
    }

    public bool Close()
    {
        _ = CloseAsync();
        return true;
    }

    public async Task<bool> CloseAsync()
    {
        if (_saving) return true;
        StopPreview();
        if (!_dirty)
        {
            _close();
            return true;
        }

        _saving = true;
        IsEnabled = false;
        Say("Salvando música…");
        try
        {
            await _preview;
            var source = Song.ToJson();
            var wav = _path[..^".song.json".Length] + ".wav";
            int? fallbackPattern = Song.Order.Count == 0 ? Pattern : null;
            await Task.Run(() =>
            {
                var song = MusicSong.Parse(source);
                var audio = new MusicRenderer().RenderWav(song, fallbackPattern);
                _storage.WriteBundle((_path, System.Text.Encoding.UTF8.GetBytes(source)), (wav, audio));
            });
            _dirty = false;
        }
        catch (Exception e) when (IsAudioError(e))
        {
            Say("Não foi possível salvar: " + e.Message, error: true);
            return true;
        }
        finally
        {
            _saving = false;
            IsEnabled = true;
        }
        _close();
        return true;
    }

    private static bool IsAudioError(Exception e) =>
        e is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or JsonException;

    private void Say(string message, bool error = false)
    {
        _status.Text = message;
        _status.IsVisible = message.Length != 0;
        _status.Foreground = LunetTokens.Brush(error ? LunetTokens.Color.Danger : LunetTokens.Color.TextSecondary);
    }

    public static string NewPath(LunetShell shell)
    {
        var files = shell.Workspace.Files();
        var n = 1;
        while (files.Contains($"Content/Audio/musica-{n}.song.json")) n++;
        return $"Content/Audio/musica-{n}.song.json";
    }

    private void UpdateInfo()
    {
        var pattern = Song.Patterns[Pattern];
        _info.Text = $"{Song.Tracks[Track].Name} · padrão {pattern.Name} ({pattern.Steps} passos) · {Song.Bpm:0} BPM · {Song.DurationSeconds:0} s";
    }

    private void Refresh()
    {
        _roll.Show(Song, Pattern, Track);
        UpdateInfo();
        _tracks.Children.Clear();
        for (var i = 0; i < Song.Tracks.Count; i++)
        {
            var index = i;
            var chip = LunetUi.Button(Song.Tracks[i].Name, i == Track ? ButtonKind.Selected : ButtonKind.Secondary, null, () => SelectTrack(index));
            chip.MinHeight = 40;
            chip.Name = "Track-" + i;
            _tracks.Children.Add(chip);
        }
        _patterns.Children.Clear();
        for (var i = 0; i < Song.Patterns.Count; i++)
        {
            var index = i;
            var chip = LunetUi.Button(Song.Patterns[i].Name, i == Pattern ? ButtonKind.Selected : ButtonKind.Ghost, null, () => SelectPattern(index));
            chip.MinHeight = 40;
            chip.Name = "Pattern-" + i;
            _patterns.Children.Add(chip);
        }
        var add = LunetUi.Button("Novo", ButtonKind.Ghost, LunetIcons.Plus, () => AddPattern(duplicate: false));
        add.MinHeight = 40;
        _patterns.Children.Add(add);
        var dup = LunetUi.Button("Duplicar", ButtonKind.Ghost, LunetIcons.Copy, () => AddPattern(duplicate: true));
        dup.MinHeight = 40;
        _patterns.Children.Add(dup);
        if (Song.Tracks.Count < MusicSong.MaxTracks)
        {
            var addTrack = LunetUi.Button("Faixa", ButtonKind.Ghost, LunetIcons.Plus, () => AddTrack());
            addTrack.MinHeight = 40;
            addTrack.Name = "AddTrack";
            _tracks.Children.Add(addTrack);
        }

        _panelTabs.Children.Clear();
        foreach (var (panel, label) in new[] { (MusicPanel.Notes, "Notas"), (MusicPanel.Track, "Faixa"), (MusicPanel.Arrangement, "Arranjo") })
        {
            var p = panel;
            var tab = LunetUi.Button(label, panel == Panel ? ButtonKind.Secondary : ButtonKind.Ghost, null, () => ShowPanel(p));
            tab.MinHeight = 40;
            tab.Name = "Panel-" + panel;
            if (panel == Panel) tab.Foreground = LunetTokens.Brush(LunetTokens.Color.Primary);
            _panelTabs.Children.Add(tab);
        }

        _panel.Children.Clear();
        switch (Panel)
        {
            case MusicPanel.Notes: NotesPanel(); break;
            case MusicPanel.Track: TrackPanel(); break;
            default: ArrangementPanel(); break;
        }
    }

    private StackPanel Chips() => new() { Orientation = Orientation.Horizontal, Spacing = 6 };

    private ScrollViewer Scroll(Control content) => new()
    {
        Content = content, Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs),
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
    };

    private void NotesPanel()
    {
        _panel.Children.Add(_tracksRow);
        _panel.Children.Add(_patternsRow);
        var lengths = Chips();
        var caption = LunetUi.Text("Duração", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
        caption.VerticalAlignment = VerticalAlignment.Center;
        caption.Margin = new Thickness(LunetTokens.Space.Xs, 0);
        lengths.Children.Add(caption);
        foreach (var (steps, label) in new[] { (1, "1"), (2, "2"), (4, "4"), (8, "8"), (16, "16") })
        {
            var n = steps;
            var chip = LunetUi.Button(label, n == NoteLength ? ButtonKind.Selected : ButtonKind.Ghost, null, () => SetNoteLength(n));
            chip.MinHeight = 40; chip.MinWidth = 44;
            chip.Name = "Length-" + n;
            AutomationProperties.SetName(chip, $"Notas de {n} passo{(n > 1 ? "s" : "")}");
            lengths.Children.Add(chip);
        }
        var down = LunetUi.Button("−8ª", ButtonKind.Ghost, null, () => ShiftOctave(-12));
        var up = LunetUi.Button("+8ª", ButtonKind.Ghost, null, () => ShiftOctave(12));
        AutomationProperties.SetName(down, "Oitava abaixo");
        AutomationProperties.SetName(up, "Oitava acima");
        _panel.Children.Add(Scroll(lengths));
        var pitches = Chips();
        var range = LunetUi.Secondary($"{MusicTheory.Name(_roll.LowestPitch)}–{MusicTheory.Name(_roll.PitchOfRow(0))}");
        range.VerticalAlignment = VerticalAlignment.Center;
        pitches.Children.Add(down);
        pitches.Children.Add(range);
        pitches.Children.Add(up);
        _panel.Children.Add(Scroll(pitches));
    }

    private void TrackPanel()
    {
        var track = Song.Tracks[Track];
        _panel.Children.Add(_tracksRow);
        var instruments = Chips();
        foreach (var preset in Instrument.Presets)
        {
            var name = preset.Name;
            var chip = LunetUi.Button(name, name == track.Instrument.Name ? ButtonKind.Selected : ButtonKind.Secondary, null, () =>
            {
                SetInstrument(name);
                PreviewNote(Math.Min(127, Song.Root + 12));
            });
            chip.MinHeight = 40;
            chip.Name = "Instrument-" + name;
            instruments.Children.Add(chip);
        }
        _panel.Children.Add(Scroll(instruments));
        Control SliderRow(string label, double min, double max, double value, Action<double> changed, string name)
        {
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, MinHeight = 40, Name = name };
            AutomationProperties.SetName(slider, label);
            slider.ValueChanged += (_, e) => changed(e.NewValue);
            var t = LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
            t.Width = 84; t.VerticalAlignment = VerticalAlignment.Center;
            var row = new DockPanel { Margin = new Thickness(LunetTokens.Space.M, 0) };
            DockPanel.SetDock(t, Dock.Left);
            row.Children.Add(t);
            row.Children.Add(slider);
            return row;
        }
        _panel.Children.Add(SliderRow("Volume", 0, 1, track.Volume, v => SetVolume((float)v), "TrackVolume"));
        _panel.Children.Add(SliderRow("Esq · Dir", -1, 1, track.Pan, v => SetPan((float)v), "TrackPan"));
        var toggles = Chips();
        var mute = LunetUi.Button(track.Muted ? "Mudo" : "Som ligado", track.Muted ? ButtonKind.Selected : ButtonKind.Secondary, null, ToggleMute);
        mute.MinHeight = 40; mute.Name = "TrackMute";
        var solo = LunetUi.Button("Solo", track.Solo ? ButtonKind.Selected : ButtonKind.Secondary, null, ToggleSolo);
        solo.MinHeight = 40; solo.Name = "TrackSolo";
        toggles.Children.Add(mute);
        toggles.Children.Add(solo);
        _panel.Children.Add(Scroll(toggles));
    }

    private void ArrangementPanel()
    {
        var order = Chips();
        for (var i = 0; i < Song.Order.Count; i++)
        {
            var index = i;
            var selected = _orderSelected == i;
            var chip = LunetUi.Button(Song.Patterns[Song.Order[i]].Name, selected ? ButtonKind.Selected : ButtonKind.Secondary, null, () => { _orderSelected = _orderSelected == index ? null : index; Refresh(); });
            chip.MinHeight = 40; chip.MinWidth = 44;
            chip.Name = "Order-" + i;
            AutomationProperties.SetName(chip, $"Posição {i + 1}: padrão {Song.Patterns[Song.Order[i]].Name}");
            order.Children.Add(chip);
        }
        var append = LunetUi.Button("+ " + Song.Patterns[Pattern].Name, ButtonKind.Ghost, null, () => AppendToOrder(Pattern));
        append.MinHeight = 40; append.Name = "AppendOrder";
        AutomationProperties.SetName(append, "Pôr o padrão " + Song.Patterns[Pattern].Name + " no fim");
        order.Children.Add(append);
        _panel.Children.Add(Scroll(order));
        if (_orderSelected is { } sel && sel < Song.Order.Count)
        {
            var actions = Chips();
            void Act(string label, Action act, string name) { var b = LunetUi.Button(label, ButtonKind.Ghost, null, act); b.MinHeight = 40; b.Name = name; actions.Children.Add(b); }
            Act("← Antes", () => MoveOrder(sel, -1), "OrderEarlier");
            Act("Depois →", () => MoveOrder(sel, 1), "OrderLater");
            Act("Tirar", () => RemoveOrder(sel), "OrderRemove");
            _panel.Children.Add(Scroll(actions));
        }
        else _panel.Children.Add(new Border { Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.Xs), Child = LunetUi.Text(Song.Order.Count == 0 ? "O arranjo está vazio: Música toca o padrão atual." : "Toque num padrão do arranjo para mudar a ordem.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary) });
        _panel.Children.Add(_bpmRow);
    }
}
