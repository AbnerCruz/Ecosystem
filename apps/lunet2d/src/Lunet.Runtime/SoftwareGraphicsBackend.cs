using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Runtime;

/// <summary>
/// CPU rasterizer implementing <see cref="IGraphicsBackend"/> with OpenGL ES semantics (LUNET-482): window coordinates
/// grow upward, render-target textures keep row 0 at the bottom, scissor is in window pixels, and the five blend modes
/// match the GL backend. Lets the desktop host and automated tests run and *see* a game without a GPU; shaders are not
/// executed (the default sprite shader is used and the fact is reported in <see cref="IgnoredShaders"/>).
/// </summary>
public sealed class SoftwareGraphicsBackend : IGraphicsBackend
{
    private sealed class Surface(int width, int height)
    {
        public int Width = width, Height = height;
        public uint[] Pixels = new uint[Math.Max(1, width * height)]; // ABGR little-endian = RGBA bytes
        public TextureFilter Filter;
    }

    private readonly Dictionary<int, Surface> _textures = new();
    private readonly Dictionary<int, int> _targetTexture = new();
    private int _next = 1;
    private Surface _screen;
    private Surface _current;
    private (int X, int Y, int W, int H) _viewport;
    private DrawState _state = DrawState.Default;

    public SoftwareGraphicsBackend(int width, int height)
    {
        _screen = new Surface(width, height);
        _current = _screen;
        _viewport = (0, 0, width, height);
    }

    public int Width => _screen.Width;
    public int Height => _screen.Height;
    public long QuadsDrawn { get; private set; }
    public int IgnoredShaders { get; private set; }

    /// <summary>Resizes the screen (call when the window changes); contents are cleared.</summary>
    public void ResizeScreen(int width, int height)
    {
        bool current = ReferenceEquals(_current, _screen);
        _screen = new Surface(width, height);
        if (current) _current = _screen;
    }

    /// <summary>The screen as RGBA rows from the top (ready for a bitmap), into <paramref name="destination"/> (Width*Height*4 bytes).</summary>
    public void CopyScreenRgba(Span<byte> destination)
    {
        int w = _screen.Width, h = _screen.Height;
        if (destination.Length < w * h * 4) throw new ArgumentException("Destino pequeno demais.", nameof(destination));
        for (int row = 0; row < h; row++)
        {
            var source = System.Runtime.InteropServices.MemoryMarshal.AsBytes(_screen.Pixels.AsSpan((h - 1 - row) * w, w));
            source.CopyTo(destination.Slice(row * w * 4, w * 4));
        }
    }

    /// <summary>Pixel of the screen as RGBA, with (0,0) at the top-left (as the player sees it).</summary>
    public Color ScreenPixel(int x, int y)
    {
        uint p = _screen.Pixels[(_screen.Height - 1 - y) * _screen.Width + x];
        return new Color((byte)p, (byte)(p >> 8), (byte)(p >> 16), (byte)(p >> 24));
    }

    public int CreateTexture(int width, int height, ReadOnlySpan<byte> rgba, TextureFilter filter)
    {
        var surface = new Surface(width, height) { Filter = filter };
        if (rgba.Length >= width * height * 4)
            System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(rgba[..(width * height * 4)]).CopyTo(surface.Pixels);
        _textures[_next] = surface;
        return _next++;
    }

    public void DeleteTexture(int handle) => _textures.Remove(handle);

    public void SetViewport(int x, int y, int width, int height) => _viewport = (x, y, Math.Max(0, width), Math.Max(0, height));

    public void Clear(Color color)
    {
        uint packed = color.PackedRgba;
        int x0 = Math.Max(0, _viewport.X), y0 = Math.Max(0, _viewport.Y);
        int x1 = Math.Min(_current.Width, _viewport.X + _viewport.W), y1 = Math.Min(_current.Height, _viewport.Y + _viewport.H);
        if (ReferenceEquals(_current, _screen) && _viewport.X == 0 && _viewport.Y == 0) { x1 = _current.Width; y1 = _current.Height; }
        for (int y = y0; y < y1; y++) _current.Pixels.AsSpan(y * _current.Width + x0, Math.Max(0, x1 - x0)).Fill(packed);
    }

    public void SetDrawState(DrawState state)
    {
        if (state.Shader != 0) IgnoredShaders++;
        _state = state;
    }

    public int CreateRenderTarget(int width, int height, TextureFilter filter, out int textureHandle)
    {
        textureHandle = CreateTexture(width, height, [], filter);
        int handle = _next++;
        _targetTexture[handle] = textureHandle;
        return handle;
    }

    public void DeleteRenderTarget(int handle) => _targetTexture.Remove(handle);

    public void SetRenderTarget(int handle, int width, int height)
    {
        if (handle == 0) { _current = _screen; return; }
        _current = _targetTexture.TryGetValue(handle, out var texture) && _textures.TryGetValue(texture, out var surface) ? surface : _screen;
    }

    public int CreateShader(string fragmentSource) => _next++;
    public void DeleteShader(int handle) { }

    public void DrawQuads(int textureHandle, ReadOnlySpan<SpriteVertex> vertices, int quadCount, in Matrix4x4 projection)
    {
        if (!_textures.TryGetValue(textureHandle, out var texture)) return;
        var sampler = _state.Sampler;
        bool linear = (sampler?.Filter ?? texture.Filter) == TextureFilter.Linear;
        bool repeat = sampler?.Wrap == TextureWrap.Repeat;
        var (cx0, cy0, cx1, cy1) = (Math.Max(0, _viewport.X), Math.Max(0, _viewport.Y),
            Math.Min(_current.Width, _viewport.X + _viewport.W), Math.Min(_current.Height, _viewport.Y + _viewport.H));
        if (_state.Scissor is { } s)
        {
            cx0 = Math.Max(cx0, s.X); cy0 = Math.Max(cy0, s.Y);
            cx1 = Math.Min(cx1, s.Right); cy1 = Math.Min(cy1, s.Bottom);
        }
        if (cx1 <= cx0 || cy1 <= cy0) return;
        Span<Vector2> screen = stackalloc Vector2[4];
        for (int q = 0; q < quadCount; q++)
        {
            var quad = vertices.Slice(q * 4, 4);
            for (int i = 0; i < 4; i++)
            {
                var clip = Vector4.Transform(new Vector4(quad[i].Position, 0, 1), projection);
                screen[i] = new Vector2((clip.X / clip.W + 1) * 0.5f * _viewport.W + _viewport.X, (clip.Y / clip.W + 1) * 0.5f * _viewport.H + _viewport.Y);
            }
            QuadsDrawn++;
            if (AxisAligned(screen) && quad[0].Color == quad[1].Color && quad[1].Color == quad[2].Color && quad[2].Color == quad[3].Color &&
                _state.Blend is BlendMode.Alpha or BlendMode.Additive or BlendMode.Opaque or BlendMode.Premultiplied)
            {
                FastRect(screen, quad, texture, linear, repeat, cx0, cy0, cx1, cy1);
                continue;
            }
            Triangle(screen[0], screen[1], screen[2], quad[0], quad[1], quad[2], texture, linear, repeat, cx0, cy0, cx1, cy1, false);
            Triangle(screen[0], screen[2], screen[3], quad[0], quad[2], quad[3], texture, linear, repeat, cx0, cy0, cx1, cy1, true);
        }
    }

    private static bool AxisAligned(ReadOnlySpan<Vector2> s)
    {
        const float e = 1e-3f;
        return MathF.Abs(s[0].Y - s[1].Y) < e && MathF.Abs(s[1].X - s[2].X) < e && MathF.Abs(s[2].Y - s[3].Y) < e && MathF.Abs(s[3].X - s[0].X) < e;
    }

    /// <summary>
    /// The common case — an unrotated sprite with one tint — with integer blending and incremental texture coordinates.
    /// Same pixel coverage as the triangle path (pixel centers inside the rectangle).
    /// </summary>
    private void FastRect(ReadOnlySpan<Vector2> s, ReadOnlySpan<SpriteVertex> quad, Surface texture, bool linear, bool repeat,
        int cx0, int cy0, int cx1, int cy1)
    {
        float left = MathF.Min(s[0].X, s[1].X), right = MathF.Max(s[0].X, s[1].X);
        float bottom = MathF.Min(s[0].Y, s[2].Y), top = MathF.Max(s[0].Y, s[2].Y);
        int x0 = Math.Max(cx0, (int)MathF.Ceiling(left - 0.5f)), x1 = Math.Min(cx1, (int)MathF.Ceiling(right - 0.5f));
        int y0 = Math.Max(cy0, (int)MathF.Ceiling(bottom - 0.5f)), y1 = Math.Min(cy1, (int)MathF.Ceiling(top - 0.5f));
        if (x1 <= x0 || y1 <= y0) return;
        // u along screen x (from vertex 0 to 1), v along screen y (from vertex 0 to 2/3).
        float su0 = s[0].X, su1 = s[1].X, sv0 = s[0].Y, sv1 = s[3].Y;
        float u0 = quad[0].TexCoord.X, u1 = quad[1].TexCoord.X, v0 = quad[0].TexCoord.Y, v1 = quad[3].TexCoord.Y;
        float du = (u1 - u0) / (su1 - su0), dv = (v1 - v0) / (sv1 - sv0);
        uint tint = quad[0].Color;
        int tr = (int)(tint & 0xff), tg = (int)(tint >> 8 & 0xff), tb = (int)(tint >> 16 & 0xff), ta = (int)(tint >> 24);
        bool whiteTint = tint == 0xffffffff;
        var mode = _state.Blend;
        var target = _current;
        int tw = texture.Width, th = texture.Height;
        // Linear filtering at a 1:1 texel-to-pixel scale samples texel centers exactly: nearest gives the same result.
        bool nearest = !linear || MathF.Abs(MathF.Abs(du) * tw - 1) < 1e-3f && MathF.Abs(MathF.Abs(dv) * th - 1) < 1e-3f;
        var pixels = texture.Pixels;
        for (int y = y0; y < y1; y++)
        {
            float v = v0 + (y + 0.5f - sv0) * dv;
            int row = y * target.Width;
            float u = u0 + (x0 + 0.5f - su0) * du;
            for (int x = x0; x < x1; x++, u += du)
            {
                uint src;
                if (nearest)
                {
                    int ix = (int)MathF.Floor(u * tw), iy = (int)MathF.Floor(v * th);
                    if (repeat) { ix = ((ix % tw) + tw) % tw; iy = ((iy % th) + th) % th; }
                    else { ix = ix < 0 ? 0 : ix >= tw ? tw - 1 : ix; iy = iy < 0 ? 0 : iy >= th ? th - 1 : iy; }
                    src = pixels[iy * tw + ix];
                }
                else
                {
                    var (fr, fg, fb, fa) = Sample(texture, u, v, true, repeat);
                    src = (uint)(fr * 255 + 0.5f) | (uint)(fg * 255 + 0.5f) << 8 | (uint)(fb * 255 + 0.5f) << 16 | (uint)(fa * 255 + 0.5f) << 24;
                }
                int r = (int)(src & 0xff), g = (int)(src >> 8 & 0xff), b = (int)(src >> 16 & 0xff), a = (int)(src >> 24);
                if (!whiteTint) { r = (r * tr + 127) / 255; g = (g * tg + 127) / 255; b = (b * tb + 127) / 255; a = (a * ta + 127) / 255; }
                if (a == 0 && mode != BlendMode.Opaque && mode != BlendMode.Premultiplied) continue;
                ref uint dst = ref target.Pixels[row + x];
                uint d = dst;
                int dr = (int)(d & 0xff), dg = (int)(d >> 8 & 0xff), db = (int)(d >> 16 & 0xff), da = (int)(d >> 24);
                int ia = 255 - a;
                switch (mode)
                {
                    case BlendMode.Opaque: break;
                    case BlendMode.Additive:
                        r = Math.Min(255, dr + (r * a + 127) / 255); g = Math.Min(255, dg + (g * a + 127) / 255);
                        b = Math.Min(255, db + (b * a + 127) / 255); a = Math.Min(255, da + a);
                        break;
                    case BlendMode.Premultiplied:
                        r = Math.Min(255, r + (dr * ia + 127) / 255); g = Math.Min(255, g + (dg * ia + 127) / 255);
                        b = Math.Min(255, b + (db * ia + 127) / 255); a = Math.Min(255, a + (da * ia + 127) / 255);
                        break;
                    default:
                        if (a == 255) break;
                        r = (r * a + dr * ia + 127) / 255; g = (g * a + dg * ia + 127) / 255;
                        b = (b * a + db * ia + 127) / 255; a = Math.Min(255, a + (da * ia + 127) / 255);
                        break;
                }
                dst = (uint)r | (uint)g << 8 | (uint)b << 16 | (uint)a << 24;
            }
        }
    }

    private void Triangle(Vector2 a, Vector2 b, Vector2 c, in SpriteVertex va, in SpriteVertex vb, in SpriteVertex vc,
        Surface texture, bool linear, bool repeat, int cx0, int cy0, int cx1, int cy1, bool second)
    {
        float area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (MathF.Abs(area) < 1e-6f) return;
        int x0 = Math.Max(cx0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int x1 = Math.Min(cx1 - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int y0 = Math.Max(cy0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int y1 = Math.Min(cy1 - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        float inv = 1 / area;
        var target = _current;
        var blend = _state.Blend;
        uint ca = va.Color, cb = vb.Color, cc = vc.Color;
        bool flatColor = ca == cb && cb == cc;
        for (int y = y0; y <= y1; y++)
        {
            float py = y + 0.5f;
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f;
                float w0 = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) * inv;
                float w1 = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) * inv;
                float w2 = 1 - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                // The quad's diagonal is shared by both triangles: the second one skips pixels exactly on it (no double blend).
                if (second && MathF.Abs(w2) < 1e-5f) continue;
                float u = w0 * va.TexCoord.X + w1 * vb.TexCoord.X + w2 * vc.TexCoord.X;
                float v = w0 * va.TexCoord.Y + w1 * vb.TexCoord.Y + w2 * vc.TexCoord.Y;
                var (tr, tg, tb, ta) = Sample(texture, u, v, linear, repeat);
                uint tint = flatColor ? ca : MixColor(ca, cb, cc, w0, w1, w2);
                float r = tr * (tint & 0xff) / 255f, g = tg * (tint >> 8 & 0xff) / 255f, bl = tb * (tint >> 16 & 0xff) / 255f, al = ta * (tint >> 24) / 255f;
                ref uint dst = ref target.Pixels[y * target.Width + x];
                dst = Blend(dst, r, g, bl, al, blend);
            }
        }
    }

    private static uint MixColor(uint a, uint b, uint c, float wa, float wb, float wc)
    {
        uint Channel(int shift) => (uint)Math.Clamp((int)MathF.Round((a >> shift & 0xff) * wa + (b >> shift & 0xff) * wb + (c >> shift & 0xff) * wc), 0, 255) << shift;
        return Channel(0) | Channel(8) | Channel(16) | Channel(24);
    }

    private static (float R, float G, float B, float A) Sample(Surface t, float u, float v, bool linear, bool repeat)
    {
        float fx = u * t.Width - (linear ? 0.5f : 0), fy = v * t.Height - (linear ? 0.5f : 0);
        if (!linear) return Texel(t, (int)MathF.Floor(fx), (int)MathF.Floor(fy), repeat);
        int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
        float ax = fx - ix, ay = fy - iy;
        var p00 = Texel(t, ix, iy, repeat); var p10 = Texel(t, ix + 1, iy, repeat);
        var p01 = Texel(t, ix, iy + 1, repeat); var p11 = Texel(t, ix + 1, iy + 1, repeat);
        float L(float q00, float q10, float q01, float q11) => (q00 * (1 - ax) + q10 * ax) * (1 - ay) + (q01 * (1 - ax) + q11 * ax) * ay;
        return (L(p00.R, p10.R, p01.R, p11.R), L(p00.G, p10.G, p01.G, p11.G), L(p00.B, p10.B, p01.B, p11.B), L(p00.A, p10.A, p01.A, p11.A));
    }

    private static (float R, float G, float B, float A) Texel(Surface t, int x, int y, bool repeat)
    {
        if (repeat) { x = ((x % t.Width) + t.Width) % t.Width; y = ((y % t.Height) + t.Height) % t.Height; }
        else { x = Math.Clamp(x, 0, t.Width - 1); y = Math.Clamp(y, 0, t.Height - 1); }
        uint p = t.Pixels[y * t.Width + x];
        return ((p & 0xff) / 255f, (p >> 8 & 0xff) / 255f, (p >> 16 & 0xff) / 255f, (p >> 24) / 255f);
    }

    private static uint Blend(uint dst, float r, float g, float b, float a, BlendMode mode)
    {
        float dr = (dst & 0xff) / 255f, dg = (dst >> 8 & 0xff) / 255f, db = (dst >> 16 & 0xff) / 255f, da = (dst >> 24) / 255f;
        (float R, float G, float B, float A) o = mode switch
        {
            BlendMode.Opaque => (r, g, b, a),
            BlendMode.Additive => (dr + r * a, dg + g * a, db + b * a, da + a),
            BlendMode.Multiply => (dr * r, dg * g, db * b, da),
            BlendMode.Premultiplied => (r + dr * (1 - a), g + dg * (1 - a), b + db * (1 - a), a + da * (1 - a)),
            _ => (r * a + dr * (1 - a), g * a + dg * (1 - a), b * a + db * (1 - a), a + da * (1 - a)),
        };
        static uint B(float v) => (uint)Math.Clamp((int)MathF.Round(v * 255), 0, 255);
        return B(o.R) | B(o.G) << 8 | B(o.B) << 16 | B(o.A) << 24;
    }
}
