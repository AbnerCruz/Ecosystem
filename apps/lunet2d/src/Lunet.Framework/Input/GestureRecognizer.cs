using System.Numerics;

namespace Lunet.Input;

/// <summary>
/// Reconhece gestos a partir dos toques de cada quadro. Independe da fase informada:
/// um dedo aparece quando surge um id ativo e termina quando some ou é solto.
/// Com dois dedos ativos emite Pinch e Rotate (e suprime os gestos de um dedo).
/// </summary>
public sealed class GestureRecognizer
{
    private sealed class Track
    {
        public int Id;
        public Vector2 Start, Last, Current, Prev;
        public double StartTime, LastTime;
        public Vector2 Velocity;
        public bool Dragging, LongPressed, Seen, MultiTouch, Released;
    }

    private readonly List<Track> _tracks = [];
    private readonly List<Track> _ended = [];
    private float _lastPairDistance;
    private float _lastPairAngle;
    private bool _pairActive;
    private double _lastTapTime = double.NegativeInfinity;
    private Vector2 _lastTapPosition;

    /// <summary>Distância (unidades virtuais) a partir da qual o toque vira arrasto.</summary>
    public float DragThreshold { get; set; } = 10f;

    /// <summary>Duração máxima de um toque para contar como Tap.</summary>
    public double TapMaxSeconds { get; set; } = 0.3;
    /// <summary>Tempo parado para virar LongPress.</summary>
    public double LongPressSeconds { get; set; } = 0.5;

    /// <summary>Tempo máximo entre dois toques para formar um DoubleTap.</summary>
    public double DoubleTapSeconds { get; set; } = 0.3;

    /// <summary>Distância máxima entre os dois toques de um DoubleTap.</summary>
    public float DoubleTapDistance { get; set; } = 30f;

    /// <summary>Velocidade mínima (unidades/s) na soltura para ser Swipe.</summary>
    public float SwipeMinSpeed { get; set; } = 600f;

    /// <summary>Processa os toques do quadro e acrescenta os gestos reconhecidos.</summary>
    /// <param name="touches">Toques do quadro.</param>
    /// <param name="now">Tempo atual em segundos.</param>
    /// <param name="output">Lista que recebe os gestos.</param>
    public void Update(ReadOnlySpan<TouchPoint> touches, double now, List<Gesture> output)
    {
        foreach (var track in _tracks) track.Seen = false;

        foreach (var touch in touches)
        {
            var track = FindTrack(touch.Id);
            if (touch.IsDown)
            {
                if (track is null)
                {
                    track = new Track { Id = touch.Id, Start = touch.Position, Last = touch.Position, Current = touch.Position, Prev = touch.Position, StartTime = now, LastTime = now };
                    _tracks.Add(track);
                }
                track.Seen = true;
                track.Current = touch.Position;
            }
            else if (track is not null && touch.Phase == TouchPhase.Released)
            {
                track.Seen = true;
                track.Current = touch.Position;
                track.Released = true;
            }
            else if (track is not null)
            {
                _ended.Add(track); // cancelado: descarta sem gesto
            }
        }

        Track? first = null, second = null;
        foreach (var t in _tracks)
        {
            if (!t.Seen || t.Released || _ended.Contains(t)) continue;
            if (first is null) first = t;
            else second ??= t;
        }
        if (first is not null && second is not null)
        {
            // Com mais de dois dedos, todos são marcados como multitoque; o par usado é o dos dois primeiros.
            foreach (var t in _tracks)
                if (t.Seen && !t.Released && !_ended.Contains(t)) t.MultiTouch = true;
            UpdatePair(first, second, output);
        }
        else
        {
            _pairActive = false;
        }

        foreach (var track in _tracks)
        {
            if (_ended.Contains(track)) continue;
            if (!track.Seen || track.Released)
            {
                if (!track.MultiTouch) { AdvanceSingle(track, track.Current, now, output); Finish(track, now, output); }
                _ended.Add(track);
            }
            else if (!track.MultiTouch)
            {
                AdvanceSingle(track, track.Current, now, output);
            }
        }

        foreach (var track in _ended) _tracks.Remove(track);
        _ended.Clear();
    }

    private Track? FindTrack(int id)
    {
        foreach (var t in _tracks)
            if (t.Id == id) return t;
        return null;
    }

    private void UpdatePair(Track a, Track b, List<Gesture> output)
    {
        var delta = b.Current - a.Current;
        var distance = delta.Length();
        var angle = MathF.Atan2(delta.Y, delta.X);
        var center = (a.Current + b.Current) * 0.5f;
        if (!_pairActive)
        {
            _pairActive = true;
            _lastPairDistance = distance;
            _lastPairAngle = angle;
            return;
        }
        if (_lastPairDistance > 0 && MathF.Abs(distance - _lastPairDistance) > 0.5f)
        {
            output.Add(new Gesture(GestureType.Pinch, a.Id, center, Vector2.Zero, Vector2.Zero, scale: distance / _lastPairDistance));
            _lastPairDistance = distance;
        }
        var rotation = MathEx.AngleDifference(_lastPairAngle, angle);
        if (MathF.Abs(rotation) > 0.01f)
        {
            output.Add(new Gesture(GestureType.Rotate, a.Id, center, Vector2.Zero, Vector2.Zero, rotation: rotation));
            _lastPairAngle = angle;
        }
    }

    private void AdvanceSingle(Track track, Vector2 position, double now, List<Gesture> output)
    {
        var dt = now - track.LastTime;
        if (dt > 0)
        {
            var instant = (position - track.Prev) / (float)dt;
            track.Velocity = track.Velocity == Vector2.Zero ? instant : Vector2.Lerp(track.Velocity, instant, 0.5f);
            track.LastTime = now;
        }
        track.Prev = position;

        if (!track.Dragging && Vector2.Distance(track.Start, position) > DragThreshold) track.Dragging = true;
        if (track.Dragging)
        {
            var delta = position - track.Last;
            if (delta != Vector2.Zero) output.Add(new Gesture(GestureType.Drag, track.Id, position, delta, track.Velocity));
            track.Last = position;
        }
        else if (!track.LongPressed && now - track.StartTime >= LongPressSeconds)
        {
            track.LongPressed = true;
            output.Add(new Gesture(GestureType.LongPress, track.Id, position, Vector2.Zero, Vector2.Zero));
        }
    }

    private void Finish(Track track, double now, List<Gesture> output)
    {
        if (track.Dragging)
        {
            if (track.Velocity.Length() >= SwipeMinSpeed)
                output.Add(new Gesture(GestureType.Swipe, track.Id, track.Current, track.Current - track.Start, track.Velocity));
        }
        else if (!track.LongPressed && now - track.StartTime <= TapMaxSeconds)
        {
            output.Add(new Gesture(GestureType.Tap, track.Id, track.Current, Vector2.Zero, Vector2.Zero));
            if (now - _lastTapTime <= DoubleTapSeconds && Vector2.Distance(_lastTapPosition, track.Current) <= DoubleTapDistance)
            {
                output.Add(new Gesture(GestureType.DoubleTap, track.Id, track.Current, Vector2.Zero, Vector2.Zero));
                _lastTapTime = double.NegativeInfinity; // o terceiro toque não forma outro DoubleTap
            }
            else
            {
                _lastTapTime = now;
                _lastTapPosition = track.Current;
            }
        }
    }

    /// <summary>Esquece os dedos e gestos em andamento.</summary>
    public void Reset()
    {
        _tracks.Clear();
        _ended.Clear();
        _pairActive = false;
        _lastTapTime = double.NegativeInfinity;
    }
}
