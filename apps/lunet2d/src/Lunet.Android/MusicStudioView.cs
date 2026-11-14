using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Lunet.Studio.Music;
using AColor = Android.Graphics.Color;
using AndroidMediaPlayer = global::Android.Media.MediaPlayer;
using Path = System.IO.Path;

namespace Lunet.Android;

/// <summary>
/// Music Studio (LUNET-500): compose multi-track songs on the phone. A step grid per track (scale notes, or drum lanes),
/// patterns and an arrangement, synthesized instruments with a mixer, and one-tap generators for melody, chords, bass and
/// grooves. Plays what will be exported; saves the editable <c>*.song.json</c> and exports the WAV the game loads.
/// </summary>
internal sealed class MusicStudioView : LinearLayout
{
    private readonly MusicSong _song;
    private readonly Action<MusicSong> _save;
    private readonly Action<MusicSong> _export;
    private readonly Action _close;
    private readonly string _cache;
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Java.Lang.Runnable _tick;
    private readonly Java.Lang.Runnable _saveLater;
    private readonly StepGrid _grid;
    private readonly TextView _title;
    private readonly TextView _status;
    private readonly LinearLayout _tracks;
    private readonly LinearLayout _patterns;
    private readonly LinearLayout _order;
    private readonly Button _play;
    private int _track, _pattern;
    private AndroidMediaPlayer? _player;
    private string? _playingFile;
    private bool _playingSong;
    private int _playOffsetSteps;
    private CancellationTokenSource? _renderCancel;
    private int _lowNote = 48, _highNote = 84;
    private int _noteLength = 1;
    private int _seed = System.Environment.TickCount;

    public MusicStudioView(Context context, MusicSong song, string cacheDirectory, Action<MusicSong> save, Action<MusicSong> export, Action close) : base(context)
    {
        _song = song; _save = save; _export = export; _close = close; _cache = cacheDirectory;
        Orientation = Orientation.Vertical;
        SetBackgroundColor(LunetUi.Background);
        _tick = new Java.Lang.Runnable(Tick);
        _saveLater = new Java.Lang.Runnable(() => { _save(_song); _status.Text = "Salvo"; });

        var bar = LunetUi.Row(context, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.AddView(LunetUi.Icon(context, "‹", "Voltar", Close), Square(44));
        var titles = LunetUi.Column(context, 0);
        _title = LunetUi.Label(context, "", LunetUi.TextLabel, LunetUi.Text, bold: true);
        _title.SetSingleLine(true);
        _status = LunetUi.Label(context, "Salvo", LunetUi.TextCaption, LunetUi.TextMuted);
        titles.AddView(_title);
        titles.AddView(_status);
        bar.AddView(titles, LunetUi.Weight());
        _play = LunetUi.Button(context, "▶ Padrão", () => TogglePlay(song: false), ButtonStyle.Primary);
        bar.AddView(_play, new LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        bar.AddView(LunetUi.Button(context, "▶ Música", () => TogglePlay(song: true), ButtonStyle.Secondary), new LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        bar.AddView(LunetUi.Icon(context, "⋯", "Mais opções", ShowSongMenu), Square(44));
        AddView(bar);

        _tracks = Strip(context, "Faixas");
        _patterns = Strip(context, "Padrões");
        _order = Strip(context, "Arranjo");

        _grid = new StepGrid(context, this);
        var vertical = new ScrollView(context) { FillViewport = true };
        var horizontal = new HorizontalScrollView(context) { FillViewport = true };
        horizontal.AddView(_grid);
        vertical.AddView(horizontal);
        AddView(vertical, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        var tools = LunetUi.Row(context, 6);
        tools.SetPadding(Dp(8), Dp(6), Dp(8), Dp(8));
        void Tool(string label, Action run) => tools.AddView(LunetUi.Button(context, label, run, ButtonStyle.Secondary));
        Tool("✦ Melodia", GenerateMelody);
        Tool("Acordes", () => GenerateChords(arpeggio: false));
        Tool("Arpejo", () => GenerateChords(arpeggio: true));
        Tool("Baixo", GenerateBass);
        Tool("Batida", GenerateGroove);
        Tool("Duração: 1", CycleLength);
        Tool("Limpar faixa", ClearTrack);
        var toolScroll = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
        toolScroll.AddView(tools);
        AddView(LunetUi.Divider(context));
        AddView(toolScroll);
        Refresh();
    }

    private int Dp(int value) => LunetUi.Dp(Context!, value);
    private LayoutParams Square(int dp) => new(Dp(dp), Dp(dp));

    private LinearLayout Strip(Context context, string label)
    {
        var row = LunetUi.Row(context, 6);
        row.SetPadding(Dp(8), Dp(2), Dp(8), Dp(2));
        row.AddView(LunetUi.Label(context, label, LunetUi.TextCaption, LunetUi.TextMuted), new LayoutParams(Dp(64), ViewGroup.LayoutParams.WrapContent));
        var items = LunetUi.Row(context, 6);
        var scroll = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
        scroll.AddView(items);
        row.AddView(scroll, LunetUi.Weight());
        AddView(row);
        return items;
    }

    private MusicPattern Pattern => _song.Patterns[_pattern];
    private MusicTrack Track => _song.Tracks[_track];
    private MusicScale Scale => MusicTheory.Scale(_song.Scale);

    // ---------- Rendering of strips ----------

    private void Refresh()
    {
        _track = Math.Clamp(_track, 0, _song.Tracks.Count - 1);
        _pattern = Math.Clamp(_pattern, 0, _song.Patterns.Count - 1);
        _title.Text = $"{_song.Name} · {_song.Bpm:0} BPM · {MusicTheory.NoteNames[_song.Root % 12]} {Scale.Name.Split(' ')[0].ToLowerInvariant()}";
        _tracks.RemoveAllViews();
        for (int i = 0; i < _song.Tracks.Count; i++)
        {
            int index = i;
            var t = _song.Tracks[i];
            var chip = LunetUi.Chip(Context!, (t.Muted ? "🔇 " : t.Solo ? "🎧 " : "") + t.Name + " · " + t.Instrument.Name, () => { _track = index; Refresh(); }, selected: i == _track);
            chip.LongClick += (_, e) => { _track = index; Refresh(); ShowTrackMenu(); e.Handled = true; };
            _tracks.AddView(chip);
        }
        if (_song.Tracks.Count < MusicSong.MaxTracks) _tracks.AddView(LunetUi.Chip(Context!, "＋ Faixa", AddTrack));

        _patterns.RemoveAllViews();
        for (int i = 0; i < _song.Patterns.Count; i++)
        {
            int index = i;
            var chip = LunetUi.Chip(Context!, $"{_song.Patterns[i].Name} ({_song.Patterns[i].Steps})", () => { _pattern = index; Refresh(); }, selected: i == _pattern);
            chip.LongClick += (_, e) => { _pattern = index; Refresh(); ShowPatternMenu(); e.Handled = true; };
            _patterns.AddView(chip);
        }
        if (_song.Patterns.Count < MusicSong.MaxPatterns) _patterns.AddView(LunetUi.Chip(Context!, "＋ Padrão", AddPattern));

        _order.RemoveAllViews();
        for (int i = 0; i < _song.Order.Count; i++)
        {
            int position = i;
            var chip = LunetUi.Chip(Context!, _song.Patterns[_song.Order[i]].Name, () => { _pattern = _song.Order[position]; Refresh(); });
            chip.LongClick += (_, e) => { _song.Order.RemoveAt(position); Changed(); e.Handled = true; };
            _order.AddView(chip);
        }
        if (_song.Order.Count < MusicSong.MaxOrder)
            _order.AddView(LunetUi.Chip(Context!, "＋ " + Pattern.Name, () => { _song.Order.Add(_pattern); Changed(); }));
        _order.AddView(LunetUi.Label(Context!, $"  {FormatTime(_song.DurationSeconds)} · segure para remover", LunetUi.TextCaption, LunetUi.TextFaint));
        _grid.Rebuild();
    }

    private static string FormatTime(double seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    private void Changed()
    {
        _status.Text = "Alterações não salvas…";
        _handler.RemoveCallbacks(_saveLater);
        _handler.PostDelayed(_saveLater, 800);
        Refresh();
    }

    // ---------- Notes ----------

    /// <summary>Rows of the grid, top to bottom: drum lanes, or scale notes from high to low.</summary>
    internal IReadOnlyList<(int Pitch, string Label, bool Root)> Rows()
    {
        if (Track.Instrument.Drums) return MusicTheory.DrumLanes.Select(l => (l.Pitch, l.Name, false)).ToArray();
        return MusicTheory.ScaleNotes(_song.Root, Scale, _lowNote, _highNote).Reverse()
            .Select(n => (n, MusicTheory.Name(n), (n - _song.Root) % 12 == 0)).ToArray();
    }

    internal int Steps => Pattern.Steps;
    internal int StepsPerBeat => _song.StepsPerBeat;
    internal int CurrentTrack => _track;
    internal IEnumerable<MusicNote> NotesOfPattern => Pattern.Notes;
    internal int PlayheadStep { get; private set; } = -1;

    internal void ToggleNote(int step, int pitch)
    {
        var existing = Pattern.Notes.FirstOrDefault(n => n.Track == _track && n.Pitch == pitch && step >= n.Step && step < n.Step + n.Length);
        if (existing is not null) Pattern.Notes.Remove(existing);
        else
        {
            int length = Track.Instrument.Drums ? 1 : Math.Min(_noteLength, Pattern.Steps - step);
            Pattern.Notes.Add(new MusicNote(_track, step, pitch, length, step % _song.StepsPerBeat == 0 ? 0.9f : 0.75f));
            PreviewNote(pitch);
        }
        Changed();
    }

    internal void StretchNote(int fromStep, int pitch, int toStep)
    {
        var note = Pattern.Notes.FirstOrDefault(n => n.Track == _track && n.Pitch == pitch && n.Step == fromStep);
        if (note is null || Track.Instrument.Drums) return;
        int length = Math.Clamp(toStep - fromStep + 1, 1, Pattern.Steps - fromStep);
        if (length == note.Length) return;
        Pattern.Notes[Pattern.Notes.IndexOf(note)] = note with { Length = length };
        Changed();
    }

    private void CycleLength()
    {
        _noteLength = _noteLength switch { 1 => 2, 2 => 4, 4 => 8, _ => 1 };
        Toast.MakeText(Context, $"Notas novas com {_noteLength} passo(s). Arraste para a direita numa nota para esticar.", ToastLength.Short)?.Show();
    }

    private void ClearTrack()
    {
        Pattern.Notes.RemoveAll(n => n.Track == _track);
        Changed();
    }

    // ---------- Generators ----------

    private int[] AskDegrees() => MusicTheory.Progressions.FirstOrDefault(p => p.Id == _lastProgression).Degrees ?? [1, 5, 6, 4];
    private string _lastProgression = "pop";

    private void WithProgression(Action<int[]> run)
    {
        var items = MusicTheory.Progressions.Select(p => p.Name).ToArray();
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Sequência de acordes")!
            .SetItems(items, (_, e) => { _lastProgression = MusicTheory.Progressions[e.Which].Id; run(AskDegrees()); })!
            .Show();
    }

    private int StepsPerChord(int chords) => Math.Max(1, Pattern.Steps / Math.Max(1, chords));

    private void Replace(IEnumerable<MusicNote> notes)
    {
        Pattern.Notes.RemoveAll(n => n.Track == _track);
        Pattern.Notes.AddRange(notes.Where(n => n.Step < Pattern.Steps).Select(n => n with { Track = _track, Length = Math.Min(n.Length, Pattern.Steps - n.Step) }));
        Changed();
    }

    private void GenerateMelody()
    {
        if (Track.Instrument.Drums) { GenerateGroove(); return; }
        WithProgression(degrees =>
        {
            _seed++;
            Replace(MusicTheory.Melody(_seed, _song.Root, Scale, degrees, StepsPerChord(degrees.Length), low: Math.Max(_lowNote, _song.Root), high: Math.Min(_highNote, _song.Root + 19)));
            _status.Text = "Melodia nova (toque de novo para outra variação)";
        });
    }

    private void GenerateChords(bool arpeggio)
    {
        if (Track.Instrument.Drums) return;
        WithProgression(degrees => Replace(MusicTheory.ChordTrack(_song.Root, Scale, degrees, StepsPerChord(degrees.Length), arpeggio)));
    }

    private void GenerateBass()
    {
        if (Track.Instrument.Drums) return;
        WithProgression(degrees => Replace(MusicTheory.BassTrack(_song.Root, Scale, degrees, StepsPerChord(degrees.Length), walking: _seed++ % 2 == 0)));
    }

    private void GenerateGroove()
    {
        if (!Track.Instrument.Drums)
        {
            Toast.MakeText(Context, "Selecione uma faixa de bateria (ou crie uma em ＋ Faixa).", ToastLength.Long)?.Show();
            return;
        }
        var grooves = MusicTheory.Grooves;
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Batida")!
            .SetItems(grooves.Select(g => g.Name).ToArray(), (_, e) => Replace(MusicTheory.Groove(grooves[e.Which].Id, Pattern.Steps)))!
            .Show();
    }

    // ---------- Tracks and patterns ----------

    private void AddTrack()
    {
        var presets = Instrument.Presets;
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Instrumento da nova faixa")!
            .SetItems(presets.Select(p => p.Name).ToArray(), (_, e) =>
            {
                _song.Tracks.Add(new MusicTrack { Name = presets[e.Which].Name, Instrument = presets[e.Which] });
                _track = _song.Tracks.Count - 1;
                Changed();
            })!.Show();
    }

    private void ShowTrackMenu()
    {
        var t = Track;
        var items = new (string Label, Action Run)[]
        {
            ("Instrumento: " + t.Instrument.Name, () =>
            {
                var presets = Instrument.Presets;
                Choose("Instrumento", presets.Select(p => p.Name).ToArray(), i => SetTrack(Track with { Instrument = presets[i] }));
            }),
            ($"Volume: {t.Volume * 100:0}%", () => Slider("Volume", t.Volume, 0, 1, v => SetTrack(Track with { Volume = v }))),
            ($"Pan: {(t.Pan < -0.05 ? "esquerda" : t.Pan > 0.05 ? "direita" : "centro")}", () => Slider("Pan (esquerda ↔ direita)", (t.Pan + 1) / 2, 0, 1, v => SetTrack(Track with { Pan = v * 2 - 1 }))),
            ($"Eco: {t.Echo * 100:0}%", () => Slider("Eco", t.Echo, 0, 1, v => SetTrack(Track with { Echo = v }))),
            ($"Reverb: {t.Reverb * 100:0}%", () => Slider("Reverb (sala)", t.Reverb, 0, 1, v => SetTrack(Track with { Reverb = v }))),
            ("Timbre: brilho do filtro", () => Slider("Brilho (corte do filtro)", (float)Math.Log(Track.Instrument.Cutoff / 100.0) / (float)Math.Log(200), 0, 1,
                v => SetTrack(Track with { Instrument = Track.Instrument with { Cutoff = (float)(100 * Math.Pow(200, v)) } }))),
            ("Timbre: ataque", () => Slider("Ataque (suave ↔ imediato)", 1 - Math.Min(1, Track.Instrument.Attack / 0.8f), 0, 1,
                v => SetTrack(Track with { Instrument = Track.Instrument with { Attack = Math.Max(0.001f, (1 - v) * 0.8f) } }))),
            ("Timbre: sustentação", () => Slider("Sustentação", Track.Instrument.Sustain, 0, 1,
                v => SetTrack(Track with { Instrument = Track.Instrument with { Sustain = v } }))),
            ("Timbre: vibrato", () => Slider("Vibrato", Track.Instrument.VibratoCents / 50f, 0, 1,
                v => SetTrack(Track with { Instrument = Track.Instrument with { VibratoCents = v * 50 } }))),
            (t.Muted ? "Ativar som" : "Silenciar", () => SetTrack(Track with { Muted = !Track.Muted })),
            (t.Solo ? "Tirar solo" : "Solo (ouvir só esta)", () => SetTrack(Track with { Solo = !Track.Solo })),
            ("Renomear", () => Rename(t.Name, name => SetTrack(Track with { Name = name }))),
            ("Duplicar faixa", () =>
            {
                if (_song.Tracks.Count >= MusicSong.MaxTracks) return;
                _song.Tracks.Add(Track with { Name = Track.Name + " 2" });
                int copy = _song.Tracks.Count - 1;
                foreach (var p in _song.Patterns) p.Notes.AddRange(p.Notes.Where(n => n.Track == _track).ToArray().Select(n => n with { Track = copy }));
                _track = copy;
                Changed();
            }),
            ("Excluir faixa", () =>
            {
                if (_song.Tracks.Count == 1) return;
                int removed = _track;
                _song.Tracks.RemoveAt(removed);
                foreach (var p in _song.Patterns)
                {
                    p.Notes.RemoveAll(n => n.Track == removed);
                    for (int i = 0; i < p.Notes.Count; i++)
                        if (p.Notes[i].Track > removed) p.Notes[i] = p.Notes[i] with { Track = p.Notes[i].Track - 1 };
                }
                _track = Math.Max(0, removed - 1);
                Changed();
            }),
        };
        Choose(t.Name, items.Select(i => i.Label).ToArray(), i => items[i].Run());
    }

    private void SetTrack(MusicTrack track) { _song.Tracks[_track] = track; Changed(); }

    private void AddPattern()
    {
        var name = ((char)('A' + _song.Patterns.Count % 26)).ToString();
        _song.Patterns.Add(new MusicPattern { Name = name, Steps = Pattern.Steps });
        _pattern = _song.Patterns.Count - 1;
        Changed();
    }

    private void ShowPatternMenu()
    {
        var items = new (string Label, Action Run)[]
        {
            ("Duplicar (para variar)", () =>
            {
                _song.Patterns.Add(new MusicPattern { Name = Pattern.Name + "'", Steps = Pattern.Steps, Notes = Pattern.Notes.ToList() });
                _pattern = _song.Patterns.Count - 1;
                Changed();
            }),
            ($"Tamanho: {Pattern.Steps} passos", () =>
            {
                int[] options = [8, 16, 32, 48, 64, 96, 128];
                Choose("Passos do padrão", options.Select(o => $"{o} ({o / _song.StepsPerBeat} tempos)").ToArray(), i =>
                {
                    Pattern.Steps = options[i];
                    Pattern.Notes.RemoveAll(n => n.Step >= Pattern.Steps);
                    for (int k = 0; k < Pattern.Notes.Count; k++)
                        Pattern.Notes[k] = Pattern.Notes[k] with { Length = Math.Min(Pattern.Notes[k].Length, Pattern.Steps - Pattern.Notes[k].Step) };
                    Changed();
                });
            }),
            ("Transpor +1 grau", () => Transpose(+1)),
            ("Transpor −1 grau", () => Transpose(-1)),
            ("Renomear", () => Rename(Pattern.Name, name => { Pattern.Name = name; Changed(); })),
            ("Excluir padrão", () =>
            {
                if (_song.Patterns.Count == 1) return;
                int removed = _pattern;
                _song.Patterns.RemoveAt(removed);
                _song.Order = _song.Order.Where(i => i != removed).Select(i => i > removed ? i - 1 : i).ToList();
                _pattern = Math.Max(0, removed - 1);
                Changed();
            }),
        };
        Choose(Pattern.Name, items.Select(i => i.Label).ToArray(), i => items[i].Run());
    }

    /// <summary>Moves the current track's notes by scale degrees (keeps them in the scale).</summary>
    private void Transpose(int degrees)
    {
        if (Track.Instrument.Drums) return;
        var notes = MusicTheory.ScaleNotes(_song.Root, Scale, 0, 127);
        for (int i = 0; i < Pattern.Notes.Count; i++)
        {
            var n = Pattern.Notes[i];
            if (n.Track != _track) continue;
            int index = Array.IndexOf(notes, MusicTheory.Snap(n.Pitch, _song.Root, Scale));
            if (index < 0) continue;
            Pattern.Notes[i] = n with { Pitch = notes[Math.Clamp(index + degrees, 0, notes.Length - 1)] };
        }
        Changed();
    }

    private void ShowSongMenu()
    {
        var items = new (string Label, Action Run)[]
        {
            ($"Andamento: {_song.Bpm:0} BPM", () => Slider("Andamento (BPM)", (_song.Bpm - 40) / 200f, 0, 1, v => { _song.Bpm = MathF.Round(40 + v * 200); Changed(); })),
            ("Tom: " + MusicTheory.NoteNamesPt[_song.Root % 12], () =>
                Choose("Tom (nota base)", MusicTheory.NoteNamesPt, i => { _song.Root = 60 + i; Changed(); })),
            ("Escala: " + Scale.Name, () =>
            {
                var scales = MusicTheory.Scales;
                Choose("Escala (o clima da música)", scales.Select(s => s.Name).ToArray(), i => { _song.Scale = scales[i].Id; Changed(); });
            }),
            ($"Swing: {_song.Swing * 200:0}%", () => Slider("Swing (balanço)", _song.Swing * 2, 0, 1, v => { _song.Swing = v / 2; Changed(); })),
            ($"Faixa de notas: {MusicTheory.Name(_lowNote)}–{MusicTheory.Name(_highNote)}", () =>
                Choose("Notas visíveis", ["Graves (C2–C5)", "Médias (C3–C6)", "Agudas (C4–C7)"], i => { (_lowNote, _highNote) = i switch { 0 => (36, 72), 1 => (48, 84), _ => (60, 96) }; Refresh(); })),
            ((_song.Loop ? "✓ " : "") + "Repetir sem emenda no jogo (loop)", () => { _song.Loop = !_song.Loop; Changed(); }),
            ($"Volume geral: {_song.MasterVolume * 100:0}%", () => Slider("Volume geral", _song.MasterVolume / 1.2f, 0, 1, v => { _song.MasterVolume = v * 1.2f; Changed(); })),
            ("Renomear música", () => Rename(_song.Name, name => { _song.Name = name; Changed(); })),
            ("Exportar WAV para o jogo", () => { Stop(); _save(_song); _export(_song); }),
        };
        Choose("Música", items.Select(i => i.Label).ToArray(), i => items[i].Run());
    }

    // ---------- Dialog helpers ----------

    private void Choose(string title, string[] items, Action<int> choose) =>
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle(title)!.SetItems(items, (_, e) => choose(e.Which))!.Show();

    private void Slider(string title, float value, float min, float max, Action<float> set)
    {
        var bar = new SeekBar(Context) { Max = 1000, Progress = (int)(Math.Clamp((value - min) / (max - min), 0, 1) * 1000) };
        var frame = new FrameLayout(Context);
        frame.SetPadding(Dp(20), Dp(16), Dp(20), Dp(4));
        frame.AddView(bar);
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle(title)!.SetView(frame)!
            .SetPositiveButton("OK", (_, _) => set(min + bar.Progress / 1000f * (max - min)))!
            .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void Rename(string current, Action<string> rename)
    {
        var input = LunetUi.Input(Context!, "Nome", current);
        var frame = new FrameLayout(Context);
        frame.SetPadding(Dp(20), Dp(8), Dp(20), Dp(4));
        frame.AddView(input);
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Nome")!.SetView(frame)!
            .SetPositiveButton("OK", (_, _) => { if ((input.Text ?? "").Trim() is { Length: > 0 } name) rename(name); })!
            .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    // ---------- Playback ----------

    private void PreviewNote(int pitch)
    {
        if (_player is not null) return; // do not interrupt the song
        var single = new MusicSong
        {
            Name = "nota", Bpm = _song.Bpm, StepsPerBeat = _song.StepsPerBeat, Loop = false,
            Tracks = [Track with { Muted = false, Solo = false, Echo = 0, Reverb = 0 }],
            Patterns = [new MusicPattern { Steps = 4, Notes = [new MusicNote(0, 0, pitch, Track.Instrument.Drums ? 1 : 2)] }],
            Order = [0],
        };
        PlayRendered(single, null, isPreview: true);
    }

    private void TogglePlay(bool song)
    {
        if (_player is not null && !_previewing) { Stop(); return; }
        _playingSong = song;
        if (song && _song.Order.Count == 0) { Toast.MakeText(Context, "Adicione padrões ao arranjo.", ToastLength.Short)?.Show(); return; }
        _playOffsetSteps = 0;
        _status.Text = "Preparando o som…";
        PlayRendered(_song, song ? null : _pattern, isPreview: false);
    }

    private bool _previewing;

    private void PlayRendered(MusicSong song, int? pattern, bool isPreview)
    {
        Stop();
        _renderCancel = new CancellationTokenSource();
        var token = _renderCancel.Token;
        var snapshot = MusicSong.Parse(song.ToJson()); // render a copy: editing continues meanwhile
        Task.Run(() =>
        {
            var wav = new MusicRenderer(isPreview ? 22_050 : 32_000).RenderWav(snapshot, pattern, token);
            Directory.CreateDirectory(_cache);
            var file = Path.Combine(_cache, "music-" + Guid.NewGuid().ToString("N") + ".wav");
            File.WriteAllBytes(file, wav);
            return file;
        }, token).ContinueWith(task =>
        {
            _handler.Post(() =>
            {
                if (token.IsCancellationRequested || task.IsCanceled) return;
                if (task.Exception is { } error)
                {
                    _status.Text = "Não foi possível tocar: " + error.GetBaseException().Message;
                    return;
                }
                StartPlayer(task.Result, isPreview, pattern is null && !isPreview && snapshot.Loop);
            });
        }, TaskScheduler.Default);
    }

    private void StartPlayer(string file, bool isPreview, bool loop)
    {
        var player = new AndroidMediaPlayer();
        _player = player;
        _playingFile = file;
        _previewing = isPreview;
        try
        {
            player.SetDataSource(file);
            player.Looping = !isPreview && (loop || !_playingSong);
            player.Prepared += (_, _) =>
            {
                if (!ReferenceEquals(_player, player)) return;
                player.Start();
                if (!isPreview)
                {
                    _play.Text = "■ Parar";
                    _status.Text = _playingSong ? "Tocando a música" : "Tocando o padrão " + Pattern.Name;
                    _handler.Post(_tick);
                }
            };
            player.Completion += (_, _) => { if (ReferenceEquals(_player, player)) Stop(); };
            player.PrepareAsync();
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or IOException)
        {
            _status.Text = "O aparelho não conseguiu tocar: " + ex.Message;
            Stop();
        }
    }

    private void Tick()
    {
        if (_player is null || _previewing) return;
        try
        {
            double seconds = _player.CurrentPosition / 1000.0;
            int step = (int)(seconds / _song.SecondsPerStep);
            if (_playingSong)
            {
                int offset = 0;
                foreach (var index in _song.Order)
                {
                    var steps = _song.Patterns[index].Steps;
                    if (step < offset + steps) { if (index != _pattern) { _pattern = index; Refresh(); } step -= offset; break; }
                    offset += steps;
                }
            }
            PlayheadStep = step % Math.Max(1, Pattern.Steps);
            _grid.Invalidate();
        }
        catch (Java.Lang.IllegalStateException) { return; }
        _handler.PostDelayed(_tick, 40);
    }

    public void Stop()
    {
        _renderCancel?.Cancel();
        _handler.RemoveCallbacks(_tick);
        var player = _player;
        _player = null;
        _previewing = false;
        if (player is not null)
        {
            try { player.Stop(); } catch (Java.Lang.IllegalStateException) { }
            player.Release();
            player.Dispose();
        }
        if (_playingFile is not null) { try { File.Delete(_playingFile); } catch (IOException) { } _playingFile = null; }
        PlayheadStep = -1;
        _play.Text = "▶ Padrão";
        _grid.Invalidate();
    }

    private void Close()
    {
        Stop();
        _handler.RemoveCallbacks(_saveLater);
        _save(_song);
        _close();
    }

    protected override void OnDetachedFromWindow()
    {
        Stop();
        base.OnDetachedFromWindow();
    }

    // ---------- The step grid ----------

    private sealed class StepGrid : View
    {
        private readonly MusicStudioView _owner;
        private readonly Paint _paint = new(PaintFlags.AntiAlias);
        private readonly Paint _text = new(PaintFlags.AntiAlias);
        private IReadOnlyList<(int Pitch, string Label, bool Root)> _rows = [];
        private readonly int _cell, _label;
        private int _downStep = -1, _downPitch = -1;
        private bool _dragged;

        public StepGrid(Context context, MusicStudioView owner) : base(context)
        {
            _owner = owner;
            _cell = LunetUi.Dp(context, 30);
            _label = LunetUi.Dp(context, 64);
            _text.TextSize = LunetUi.Dp(context, 11);
            _text.Color = LunetUi.TextMuted;
        }

        public void Rebuild()
        {
            _rows = _owner.Rows();
            RequestLayout();
            Invalidate();
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec) =>
            SetMeasuredDimension(_label + _owner.Steps * _cell + _cell / 2, Math.Max(_rows.Count, 1) * _cell + _cell / 2);

        protected override void OnDraw(Canvas canvas)
        {
            canvas.DrawColor(LunetUi.Background);
            int steps = _owner.Steps, beat = Math.Max(1, _owner.StepsPerBeat);
            for (int r = 0; r < _rows.Count; r++)
            {
                float top = r * _cell;
                _paint.Color = _rows[r].Root ? LunetUi.SurfaceHigh : r % 2 == 0 ? LunetUi.Surface : LunetUi.Background;
                canvas.DrawRect(_label, top, _label + steps * _cell, top + _cell, _paint);
                _text.Color = _rows[r].Root ? LunetUi.AccentText : LunetUi.TextMuted;
                canvas.DrawText(_rows[r].Label, LunetUi.Dp(Context!, 6), top + _cell * 0.66f, _text);
            }
            for (int s = 0; s <= steps; s++)
            {
                _paint.Color = s % (beat * 4) == 0 ? LunetUi.TextFaint : s % beat == 0 ? LunetUi.Border : AColor.Argb(90, LunetUi.Border.R, LunetUi.Border.G, LunetUi.Border.B);
                _paint.StrokeWidth = s % beat == 0 ? 2 : 1;
                canvas.DrawLine(_label + s * _cell, 0, _label + s * _cell, _rows.Count * _cell, _paint);
            }
            if (_owner.PlayheadStep >= 0)
            {
                _paint.Color = AColor.Argb(60, LunetUi.Accent.R, LunetUi.Accent.G, LunetUi.Accent.B);
                canvas.DrawRect(_label + _owner.PlayheadStep * _cell, 0, _label + (_owner.PlayheadStep + 1) * _cell, _rows.Count * _cell, _paint);
            }
            var rowOf = _rows.Select((row, index) => (row.Pitch, index)).ToDictionary(x => x.Pitch, x => x.index);
            float pad = LunetUi.Dp(Context!, 2);
            foreach (var note in _owner.NotesOfPattern)
            {
                bool mine = note.Track == _owner.CurrentTrack;
                if (!rowOf.TryGetValue(note.Pitch, out var row)) continue;
                if (!mine && _owner._song.Tracks[note.Track].Instrument.Drums != _owner.Track.Instrument.Drums) continue;
                _paint.Color = mine ? LunetUi.Accent : AColor.Argb(70, LunetUi.TextFaint.R, LunetUi.TextFaint.G, LunetUi.TextFaint.B);
                var rect = new RectF(_label + note.Step * _cell + pad, row * _cell + pad,
                    _label + (note.Step + note.Length) * _cell - pad, (row + 1) * _cell - pad);
                canvas.DrawRoundRect(rect, pad * 2, pad * 2, _paint);
            }
        }

        public override bool OnTouchEvent(MotionEvent? e)
        {
            if (e is null) return false;
            int step = (int)((e.GetX() - _label) / _cell);
            int row = (int)(e.GetY() / _cell);
            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    if (step < 0 || step >= _owner.Steps || row < 0 || row >= _rows.Count) return false;
                    _downStep = step; _downPitch = _rows[row].Pitch; _dragged = false;
                    Parent?.RequestDisallowInterceptTouchEvent(_owner.NotesOfPattern.Any(n => n.Track == _owner.CurrentTrack && n.Pitch == _downPitch && n.Step == _downStep));
                    return true;
                case MotionEventActions.Move:
                    if (_downStep >= 0 && step > _downStep && _owner.NotesOfPattern.Any(n => n.Track == _owner.CurrentTrack && n.Pitch == _downPitch && n.Step == _downStep))
                    {
                        _dragged = true;
                        _owner.StretchNote(_downStep, _downPitch, Math.Min(step, _owner.Steps - 1));
                    }
                    return true;
                case MotionEventActions.Up:
                    if (_downStep >= 0 && !_dragged && step == _downStep) _owner.ToggleNote(_downStep, _downPitch);
                    _downStep = -1;
                    Parent?.RequestDisallowInterceptTouchEvent(false);
                    return true;
                case MotionEventActions.Cancel:
                    _downStep = -1;
                    return true;
            }
            return base.OnTouchEvent(e);
        }
    }
}
