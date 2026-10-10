namespace Urbe.Core;

/// <summary>
/// C# port of the Urbe 1.8.4-beta world/life.js light cycle and the first
/// weather transitions. This clock advances independently of rendering.
/// Other legacy life entities/events are separate migration work, not faked.
/// </summary>
public sealed class LegacyWorldLife
{
    public readonly record struct Light(int Red, int Green, int Blue,
        double Darkness, double Hour);
    private readonly record struct Stop(double Hour, int R, int G, int B,
        double Dark);

    // Literal keyframes from src/world/life.js CHAVES (1.8.4-beta).
    private static readonly Stop[] Stops =
    [
        new(0,78,92,158,1), new(4.6,84,96,162,1),
        new(5.4,166,132,178,.72), new(6.2,246,186,166,.32),
        new(7.2,255,234,212,.06), new(8.2,255,255,255,0),
        new(15.8,255,255,255,0), new(16.6,255,244,222,0),
        new(17.3,255,214,160,.06), new(18.1,248,164,124,.3),
        new(18.9,180,120,158,.6), new(19.8,106,104,170,.88),
        new(20.6,80,94,160,1), new(24,78,92,158,1)
    ];

    public string TimeMode { get; set; } = "real";
    public double ElapsedSeconds { get; private set; }
    public double Rain { get; private set; }
    public double Fog { get; private set; }
    public bool Snow { get; private set; }
    public Light CurrentLight { get; private set; }

    private double _rainTarget, _fogTarget, _rainUntil, _fogUntil;
    private double _nextWeather = 20;

    public LegacyWorldLife() =>
        CurrentLight = LightAt(12.5);

    // Original JS Math.round uses floor(v+0.5), not bankers' rounding.
    private static int JsRound(double value) => (int)Math.Floor(value + .5);
    private static int Interpolate(int a, int b, double t) =>
        JsRound(a + (b - a) * t);

    public static Light LightAt(double hour)
    {
        if (!double.IsFinite(hour))
            throw new ArgumentOutOfRangeException(nameof(hour));
        hour = ((hour % 24) + 24) % 24;
        for (int i = 0; i < Stops.Length - 1; i++)
        {
            var a = Stops[i];
            var b = Stops[i + 1];
            if (hour < a.Hour || hour > b.Hour) continue;
            double t = (hour - a.Hour) / (b.Hour - a.Hour);
            t = t * t * (3 - 2 * t);  // exactly JS smoothstep
            return new Light(Interpolate(a.R,b.R,t),
                Interpolate(a.G,b.G,t), Interpolate(a.B,b.B,t),
                a.Dark + (b.Dark - a.Dark) * t, hour);
        }
        return new Light(255,255,255,0,hour);
    }

    public static double HourAt(DateTimeOffset localTime,
        string mode = "real")
    {
        return mode switch
        {
            "dia" => 12.5, "entardecer" => 18.2, "noite" => 23,
            // source: (Date.now()/1000 / (24*60) % 1) * 24.
            "ciclo" => (((localTime.ToUnixTimeMilliseconds()/1000d/1440d)
                % 1 + 1) % 1) * 24,
            _ => localTime.Hour + localTime.Minute/60d +
                localTime.Second/3600d + localTime.Millisecond/3_600_000d
        };
    }

    /// <summary>Start rain/fog with the same exponential easing used by life.js.</summary>
    public void StartRain(double duration, bool snow = false,
        double strength = .75)
    {
        if (duration <= 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration));
        _rainTarget = Math.Clamp(strength, .55, 1);
        _rainUntil = ElapsedSeconds + duration;
        Snow = snow;
    }

    public void StartFog(double duration, double strength = .7)
    {
        if (duration <= 0 || !double.IsFinite(duration))
            throw new ArgumentOutOfRangeException(nameof(duration));
        _fogTarget = Math.Clamp(strength, .6, 1);
        _fogUntil = ElapsedSeconds + duration;
    }

    public void Advance(double seconds, DateTimeOffset localTime)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || seconds > .1)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        ElapsedSeconds += seconds;
        if (_rainTarget > 0 && ElapsedSeconds > _rainUntil)
            _rainTarget = 0;
        if (_fogTarget > 0 && ElapsedSeconds > _fogUntil)
            _fogTarget = 0;
        Rain += (_rainTarget - Rain) * Math.Min(1, seconds * .35);
        Fog += (_fogTarget - Fog) * Math.Min(1, seconds * .25);
        if (Rain < .004 && _rainTarget == 0) Rain = 0;
        if (Fog < .004 && _fogTarget == 0) Fog = 0;

        var light = LightAt(HourAt(localTime, TimeMode));
        if (Rain > 0)
        {
            int r = JsRound(light.Red * (1 - Rain * .26));
            int g = JsRound(light.Green * (1 - Rain * .22));
            int b = JsRound(light.Blue * (1 - Rain * .14));
            light = light with
            {
                Red = r, Green = g, Blue = b,
                Darkness = Math.Min(1, light.Darkness + Rain * .12)
            };
        }
        CurrentLight = light;
    }
}
