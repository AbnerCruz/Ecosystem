using System.Numerics;

namespace Lunet.Input;

/// <summary>
/// Reconhece gestos de um dedo a partir dos toques de cada quadro. Independe da fase informada:
/// um dedo aparece quando surge um id ativo e termina quando some ou é solto.
/// </summary>
public sealed class GestureRecognizer
{
    private sealed class Track
    {
        public int Id;
        public Vector2 Start, Last, Current;
        public double StartTime, LastTime;
        public Vector2 Velocity;
        public bool Dragging, LongPressed, Seen;
    }

    private readonly List<Track> _tracks = [];
    private readonly List<Track> _ended = [];

    /// <summary>Distância (unidades virtuais) a partir da qual o toque vira arrasto.</summary>
    public float DragThreshold { get; set; } = 10f;

    public double TapMaxSeconds { get; set; } = 0.3;
    public double LongPressSeconds { get; set; } = 0.5;

    /// <summary>Velocidade mínima (unidades/s) na soltura para ser Swipe.</summary>
    public float SwipeMinSpeed { get; set; } = 600f;

    public void Update(ReadOnlySpan<TouchPoint> touches, double now, List<Gesture> output)
    {
        foreach (var track in _tracks) track.Seen = false;

        foreach (var touch in touches)
        {
            var track = _tracks.Find(t => t.Id == touch.Id);
            if (touch.IsDown)
            {
                if (track is null)
                {
                    track = new Track { Id = touch.Id, Start = touch.Position, Last = touch.Position, StartTime = now, LastTime = now };
                    _tracks.Add(track);
                }
                track.Seen = true;
                Advance(track, touch.Position, now, output);
            }
            else if (track is not null && touch.Phase == TouchPhase.Released)
            {
                track.Seen = true;
                Advance(track, touch.Position, now, output);
                Finish(track, now, output);
                _ended.Add(track);
            }
            else if (track is not null)
            {
                _ended.Add(track); // cancelado: descarta sem gesto
            }
        }

        // Dedos que sumiram sem soltura explícita são tratados como soltos.
        foreach (var track in _tracks)
            if (!track.Seen && !_ended.Contains(track)) { Finish(track, now, output); _ended.Add(track); }

        foreach (var track in _ended) _tracks.Remove(track);
        _ended.Clear();
    }

    private void Advance(Track track, Vector2 position, double now, List<Gesture> output)
    {
        var dt = now - track.LastTime;
        if (dt > 0)
        {
            var instant = (position - track.Current) / (float)dt;
            track.Velocity = track.Velocity == Vector2.Zero ? instant : Vector2.Lerp(track.Velocity, instant, 0.5f);
            track.LastTime = now;
        }
        track.Current = position;

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
        }
    }

    public void Reset()
    {
        _tracks.Clear();
        _ended.Clear();
    }
}
