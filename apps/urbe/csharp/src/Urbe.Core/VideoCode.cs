using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Urbe.Core;

/// <summary>One drawing operation recorded by a code element (replayed by the renderer, in the element's box).</summary>
public sealed record VideoDrawCommand(string Op, double[] Args, string? Text = null, string? Color = null, string? Color2 = null);

/// <summary>Why a code element could not draw: the script's own error, or a limit it went over.</summary>
public sealed class VideoCodeException(string message) : Exception(message);

/// <summary>
/// #1004 (owner: "código livre pelo agente, tal como GPT e Claude"): a video element whose drawing is a JavaScript
/// program the agent writes. The script defines <c>draw(ctx, t, info)</c>; each frame it runs in the Jint interpreter
/// (pure C#, the same on Android and Windows) with nothing of the app, the vault, the network or the files in reach:
/// only <c>ctx</c>, which records drawing commands, and plain numbers. Each frame has limits on time, statements,
/// memory and recursion; <c>Math.random</c> is seeded per element and frame, so the same video comes out every time.
/// </summary>
public static class VideoCode
{
    public const int MaxSourceChars = 60_000, MaxCommands = 60_000, MaxStatements = 5_000_000;
    /// <summary>
    /// A safety net only. The limit that decides is <see cref="MaxStatements"/>: it is the same on every device, so a
    /// program that draws here draws on a slow phone too (a wall-clock limit would cut it there and not here).
    /// </summary>
    public static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How wide a text is drawn (text, size, bold → pixels): set by the renderer with its real font; until then an
    /// estimate, so a program measures the same way wherever it runs in the app.
    /// </summary>
    public static Func<string, double, bool, double> Measure { get; set; } = (text, size, _) => text.Length * size * 0.55;

    public const string Help =
        "draw(ctx, t, info): t = segundos da cena, info = {width, height, frame, fps}. ctx desenha em pixels da caixa do elemento (0,0 no canto superior esquerdo; ctx.width × ctx.height). " +
        "Cor e traço: fill(cor) stroke(cor, largura) noFill() noStroke() alpha(0..1) blend(\"normal|add|screen|multiply|overlay|lighten|darken\") dash(traço, vão, deslocamento) (dash(0) volta ao contínuo) gradient(x1,y1,x2,y2,cor1,cor2) glow(raio, cor) noGlow(). " +
        "Formas: circle(x,y,r) ellipse(x,y,rx,ry) rect(x,y,w,h,raio?) arc(x,y,r,grauInicial,grauFinal) line(x1,y1,x2,y2) poly([x,y,…], fechar?) path(\"M… L… C… Z\") text(texto,x,y,tamanho,\"left|center|right\"?, negrito?) image(i,x,y,largura,altura) (a imagem i da lista em source, \"a.png; b.jpg\", do vault). measure(texto, tamanho, negrito?) devolve a largura em pixels. " +
        "Transformações: save() restore() translate(x,y) rotate(graus) scale(s, sy?). Também: lerp(a,b,u), clamp(v,a,b), ease(u) (suave 0..1), noise(x), Math.random() determinístico. " +
        "Simulação com estado: s = simulate(\"nome\", function(){ return estadoInicial }, function(s, dt, tempo){ avança s }, t, dt?) dá o estado em t, avançado em passos fixos (o mesmo t dá sempre o mesmo estado); dentro do passo use noise(), não Math.random(). " +
        "Sem acesso a arquivos, rede, notas ou tempo real: tudo vem de t.";

    private sealed class Prepared(Engine engine, JsValue draw, Recorder recorder)
    {
        public Engine Engine { get; } = engine;
        public JsValue Draw { get; } = draw;
        public Recorder Recorder { get; } = recorder;
    }

    // engines are not thread-safe: each thread (preview, export) keeps its own, per script
    [ThreadStatic] private static Dictionary<string, Prepared>? _prepared;

    /// <summary>Throws <see cref="VideoCodeException"/> saying what is wrong when the script cannot run.</summary>
    public static void Check(string source) => Prepare(source);

    /// <summary>The commands the script draws at <paramref name="t"/> in a <paramref name="width"/>×<paramref name="height"/> box.</summary>
    public static IReadOnlyList<VideoDrawCommand> Run(string source, double t, double width, double height, int fps = 30, string seed = "")
    {
        var p = Prepare(source);
        p.Recorder.Reset(width, height);
        Seed(p.Engine, seed + "|" + Math.Round(t * 1000));
        try
        {
            var info = new JsObject(p.Engine);
            info.Set("width", width); info.Set("height", height); info.Set("frame", Math.Round(t * fps)); info.Set("fps", fps);
            p.Engine.Advanced.ResetCallStack();
            p.Engine.Invoke(p.Draw, p.Recorder.Api, t, info);
        }
        catch (JavaScriptException e) { throw new VideoCodeException("erro no código: " + e.Message + Where(e.Location)); }
        catch (TimeoutException) { throw new VideoCodeException("o quadro levou mais de " + FrameTimeout.TotalMilliseconds + " ms; simplifique o desenho ou o laço"); }
        catch (StatementsCountOverflowException) { throw new VideoCodeException("o quadro passou de " + MaxStatements + " instruções (laço longo demais?)"); }
        catch (RecursionDepthOverflowException) { throw new VideoCodeException("recursão profunda demais"); }
        catch (MemoryLimitExceededException) { throw new VideoCodeException("o quadro usou memória demais"); }
        catch (VideoCodeLimit e) { throw new VideoCodeException(e.Message); }
        return p.Recorder.Commands.ToList();
    }

    private static string Where(in Acornima.SourceLocation l) => l.Start.Line > 0 ? " (linha " + l.Start.Line + ")" : "";

    private static Prepared Prepare(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new VideoCodeException("o código está vazio; defina function draw(ctx, t, info) { … }");
        if (source.Length > MaxSourceChars) throw new VideoCodeException("o código tem " + source.Length + " caracteres; use até " + MaxSourceChars);
        var cache = _prepared ??= new(StringComparer.Ordinal);
        if (cache.TryGetValue(source, out var ready)) return ready;
        if (cache.Count > 32) cache.Clear();
        var engine = new Engine(o => o
            .TimeoutInterval(FrameTimeout)
            .MaxStatements(MaxStatements)
            .LimitRecursion(256)
            .LimitMemory(64L << 20)
            .Strict()
            .CatchClrExceptions(e => e is VideoCodeLimit));
        var recorder = new Recorder(engine);
        try
        {
            engine.Execute(Prelude);
            engine.Execute(source);
        }
        catch (Acornima.ParseErrorException e) { throw new VideoCodeException("o código não é JavaScript válido: " + e.Description + " (linha " + e.LineNumber + ")"); }
        catch (JavaScriptException e) when (e.Error.IsObject() && e.Error.AsObject().Get("name").ToString() == "SyntaxError")
        { throw new VideoCodeException("o código não é JavaScript válido: " + e.Message + Where(e.Location)); }
        catch (JavaScriptException e) { throw new VideoCodeException("erro ao carregar o código: " + e.Message + Where(e.Location)); }
        catch (Exception e) when (e is TimeoutException or StatementsCountOverflowException or MemoryLimitExceededException or RecursionDepthOverflowException)
        { throw new VideoCodeException("o código não terminou de carregar dentro dos limites; deixe o trabalho para dentro de draw"); }
        var draw = engine.GetValue("draw");
        if (draw is not Jint.Native.Function.Function) throw new VideoCodeException("defina function draw(ctx, t, info) { … }: é ela que desenha cada quadro");
        return cache[source] = new Prepared(engine, draw, recorder);
    }

    private static void Seed(Engine engine, string seed)
    {
        ulong h = 1469598103934665603UL;
        foreach (var c in seed) { h ^= c; h *= 1099511628211UL; }
        engine.Invoke("__seed", (double)(h % 2147483647UL + 1));
    }

    // helpers in plain JavaScript; Math.random becomes a seeded generator (the same every time for the same frame)
    private const string Prelude = """
        var __s = 1;
        function __seed(s) { __s = s; }
        Math.random = function () { __s = (__s * 16807) % 2147483647; return (__s - 1) / 2147483646; };
        function lerp(a, b, u) { return a + (b - a) * u; }
        function clamp(v, a, b) { return Math.min(Math.max(v, a), b); }
        function ease(u) { u = clamp(u, 0, 1); return u * u * (3 - 2 * u); }
        function __hash(x) { var s = Math.sin(x * 127.1 + 311.7) * 43758.5453; return s - Math.floor(s); }
        function noise(x) { var i = Math.floor(x), f = x - i; f = f * f * (3 - 2 * f); return lerp(__hash(i), __hash(i + 1), f); }
        var __sim = {};
        function simulate(key, init, step, t, dt) {
          dt = dt || 1 / 60;
          var c = __sim[key];
          if (!c || c.t > t + 1e-9) c = { t: 0, s: init() };
          while (c.t + dt <= t + 1e-9) { step(c.s, dt, c.t); c.t += dt; }
          __sim[key] = c;
          return c.s;
        }
        """;

    private sealed class VideoCodeLimit(string message) : Exception(message);

    /// <summary>The ctx object: every call records a command; nothing is drawn here.</summary>
    private sealed class Recorder
    {
        public readonly List<VideoDrawCommand> Commands = [];
        public JsObject Api { get; }
        private readonly JsObject _api;

        public Recorder(Engine engine)
        {
            _api = new JsObject(engine);
            Api = _api;
            void Op(string name, int min, Func<JsValue[], VideoDrawCommand> make) =>
                _api.Set(name, new Jint.Runtime.Interop.ClrFunction(engine, name, (_, args) =>
                {
                    if (args.Length < min) throw new JavaScriptException(engine.Intrinsics.TypeError, "ctx." + name + " precisa de " + min + " argumentos");
                    if (Commands.Count >= MaxCommands) throw new VideoCodeLimit("o quadro passou de " + MaxCommands + " comandos de desenho");
                    Commands.Add(make(args));
                    return JsValue.Undefined;
                }));
            double N(JsValue[] a, int i, double d = 0) => i < a.Length && a[i].IsNumber() ? a[i].AsNumber() : d;
            string S(JsValue[] a, int i, string d = "") => i < a.Length && !a[i].IsUndefined() && !a[i].IsNull() ? TypeConverter.ToString(a[i]) : d;
            double[] Nums(JsValue[] a, int count) { var r = new double[count]; for (int i = 0; i < count; i++) r[i] = N(a, i); return r; }
            Op("fill", 1, a => new("fill", [], Color: S(a, 0)));
            Op("stroke", 1, a => new("stroke", [N(a, 1, 2)], Color: S(a, 0)));
            Op("noFill", 0, _ => new("noFill", []));
            Op("noStroke", 0, _ => new("noStroke", []));
            Op("alpha", 1, a => new("alpha", [Math.Clamp(N(a, 0, 1), 0, 1)]));
            Op("gradient", 6, a => new("gradient", Nums(a, 4), Color: S(a, 4), Color2: S(a, 5)));
            Op("glow", 1, a => new("glow", [Math.Clamp(N(a, 0), 0, 400)], Color: S(a, 1)));
            Op("noGlow", 0, _ => new("noGlow", []));
            Op("circle", 3, a => new("circle", Nums(a, 3)));
            Op("ellipse", 4, a => new("ellipse", Nums(a, 4)));
            Op("rect", 4, a => new("rect", [N(a, 0), N(a, 1), N(a, 2), N(a, 3), N(a, 4)]));
            Op("line", 4, a => new("line", Nums(a, 4)));
            Op("path", 1, a => new("path", [], Text: S(a, 0)));
            Op("text", 3, a => new("text", [N(a, 1), N(a, 2), N(a, 3, 24), a.Length > 5 && TypeConverter.ToBoolean(a[5]) ? 1 : 0], Text: S(a, 0), Color: S(a, 4, "center")));
            Op("poly", 1, a =>
            {
                if (a[0] is not JsArray arr) throw new JavaScriptException(engine.Intrinsics.TypeError, "ctx.poly precisa de uma lista [x1, y1, x2, y2, …]");
                var len = (int)Math.Min(arr.Length, 20_000);
                var pts = new double[len + 1];
                for (int i = 0; i < len; i++) pts[i] = arr[(uint)i].IsNumber() ? arr[(uint)i].AsNumber() : 0;
                pts[len] = a.Length > 1 && TypeConverter.ToBoolean(a[1]) ? 1 : 0;
                return new("poly", pts);
            });
            Op("image", 5, a => new("image", [N(a, 0), N(a, 1), N(a, 2), N(a, 3), N(a, 4)]));
            _api.Set("measure", new Jint.Runtime.Interop.ClrFunction(engine, "measure", (_, args) =>
                args.Length == 0 ? 0 : Measure(S(args, 0), Math.Max(1, N(args, 1, 24)), args.Length > 2 && TypeConverter.ToBoolean(args[2]))));
            Op("arc", 5, a => new("arc", Nums(a, 5)));
            Op("blend", 1, a => S(a, 0) is var m && m is "normal" or "add" or "screen" or "multiply" or "overlay" or "lighten" or "darken"
                ? new("blend", [], Text: m) : throw new JavaScriptException(engine.Intrinsics.TypeError, "ctx.blend: normal, add, screen, multiply, overlay, lighten ou darken"));
            Op("dash", 0, a => new("dash", [N(a, 0), N(a, 1, N(a, 0)), N(a, 2)]));
            Op("save", 0, _ => new("save", []));
            Op("restore", 0, _ => new("restore", []));
            Op("translate", 2, a => new("translate", Nums(a, 2)));
            Op("rotate", 1, a => new("rotate", Nums(a, 1)));
            Op("scale", 1, a => new("scale", [N(a, 0, 1), N(a, 1, N(a, 0, 1))]));
        }

        public void Reset(double width, double height)
        {
            Commands.Clear();
            _api.Set("width", width);
            _api.Set("height", height);
        }
    }
}
