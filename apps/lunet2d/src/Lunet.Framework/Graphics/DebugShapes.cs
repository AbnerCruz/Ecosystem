using System.Numerics;

namespace Lunet.Graphics;

public static partial class DebugDraw
{
    /// <summary>Limite de linhas por grade e de vértices por polígono de depuração.</summary>
    public const int MaxDebugSegments = 4096;

    /// <summary>Desenha contorno fechado ou polilinha, sem preencher nem alocar.</summary>
    /// <param name="batch">Lote entre Begin/End; câmera e clipping existentes são preservados.</param>
    /// <param name="vertices">Pontos locais; mínimo três se fechado, dois se aberto, máximo 4096.</param>
    /// <param name="color">Cor do contorno.</param>
    /// <param name="closed">Liga o último ponto ao primeiro quando true.</param>
    /// <param name="thickness">Espessura positiva e finita.</param>
    /// <param name="transform">Matriz local→mundo opcional; null usa identidade.</param>
    /// <remarks>Valida toda a geometria antes de desenhar. Aceita formas côncavas; não calcula colisão, preenchimento ou triangulação.</remarks>
    public static void Polygon(this SpriteBatch batch, ReadOnlySpan<Vector2> vertices, Color color, bool closed = true, float thickness = 1f, Matrix3x2? transform = null)
    {
        ValidateBatchAndThickness(batch, thickness);
        if (vertices.Length < (closed ? 3 : 2) || vertices.Length > MaxDebugSegments)
            throw new ArgumentOutOfRangeException(nameof(vertices));
        var matrix = transform ?? Matrix3x2.Identity;
        if (!Finite(matrix)) throw new ArgumentOutOfRangeException(nameof(transform));
        int segments = closed ? vertices.Length : vertices.Length - 1;
        for (int i = 0; i < segments; i++)
            ValidateSegment(Vector2.Transform(vertices[i], matrix), Vector2.Transform(vertices[(i + 1) % vertices.Length], matrix), thickness);
        for (int i = 0; i < segments; i++)
            DebugSegment(batch, Vector2.Transform(vertices[i], matrix), Vector2.Transform(vertices[(i + 1) % vertices.Length], matrix), color, thickness);
    }

    /// <summary>Desenha trecho de um Ray2D de comprimento explícito, sem alocar.</summary>
    /// <param name="batch">Lote entre Begin/End.</param>
    /// <param name="ray">Raio com origem/direção finitas e direção unitária; default é rejeitado.</param>
    /// <param name="length">Distância não negativa e finita; zero não desenha.</param>
    /// <param name="color">Cor da linha.</param>
    /// <param name="thickness">Espessura positiva e finita.</param>
    public static void Ray(this SpriteBatch batch, Ray2D ray, float length, Color color, float thickness = 1f)
    {
        ValidateBatchAndThickness(batch, thickness);
        if (!float.IsFinite(length) || length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (!Finite(ray.Origin) || !Finite(ray.Direction) || MathF.Abs(ray.Direction.LengthSquared() - 1) > 0.0001f)
            throw new ArgumentException("Raio deve ter direção unitária finita.", nameof(ray));
        var end = ray.PointAt(length);
        ValidateSegment(ray.Origin, end, thickness);
        DebugSegment(batch, ray.Origin, end, color, thickness);
    }

    /// <summary>Desenha eixos X/Y no pivô mundial do transform; respeita rotação, escala e reflexão.</summary>
    /// <param name="batch">Lote entre Begin/End.</param>
    /// <param name="transform">Transform com posição, origem, rotação e escala finitas.</param>
    /// <param name="length">Comprimento local positivo e finito; escala zero colapsa o respectivo eixo.</param>
    /// <param name="xColor">Cor do eixo X.</param>
    /// <param name="yColor">Cor do eixo Y.</param>
    /// <param name="thickness">Espessura positiva e finita.</param>
    /// <remarks>Eixos partem de Transform.Position (pivô), não do ponto local zero deslocado por Origin.</remarks>
    public static void Axes(this SpriteBatch batch, Transform2D transform, float length, Color xColor, Color yColor, float thickness = 1f)
    {
        ValidateBatchAndThickness(batch, thickness);
        if (!float.IsFinite(length) || length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (!Finite(transform.Position) || !Finite(transform.Scale) || !Finite(transform.Origin) || !float.IsFinite(transform.Rotation))
            throw new ArgumentException("Transform deve ter valores finitos.", nameof(transform));
        float cos = MathF.Cos(transform.Rotation), sin = MathF.Sin(transform.Rotation);
        var x = transform.Position + new Vector2(cos, sin) * (length * transform.Scale.X);
        var y = transform.Position + new Vector2(-sin, cos) * (length * transform.Scale.Y);
        ValidateSegment(transform.Position, x, thickness); ValidateSegment(transform.Position, y, thickness);
        DebugSegment(batch, transform.Position, x, xColor, thickness);
        DebugSegment(batch, transform.Position, y, yColor, thickness);
    }

    /// <summary>Desenha grade limitada à área, incluindo bordas, sem alocar. Origem é o canto da área.</summary>
    /// <param name="batch">Lote entre Begin/End.</param>
    /// <param name="area">Retângulo finito com dimensões não negativas; área zero não desenha.</param>
    /// <param name="spacing">Passo X/Y positivo e finito.</param>
    /// <param name="color">Cor das linhas.</param>
    /// <param name="thickness">Espessura positiva e finita.</param>
    /// <remarks>Rejeita mais de 4096 linhas antes de desenhar. Bordas não são repetidas quando coincidem com a grade. Não recorta outros desenhos.</remarks>
    public static void Grid(this SpriteBatch batch, RectangleF area, Vector2 spacing, Color color, float thickness = 1f)
    {
        ValidateBatchAndThickness(batch, thickness);
        if (!Finite(area.Position) || !float.IsFinite(area.Width) || !float.IsFinite(area.Height) || area.Width < 0 || area.Height < 0 || !float.IsFinite(area.Right) || !float.IsFinite(area.Bottom))
            throw new ArgumentOutOfRangeException(nameof(area));
        if (!Finite(spacing) || spacing.X <= 0 || spacing.Y <= 0) throw new ArgumentOutOfRangeException(nameof(spacing));
        if (area.Width == 0 || area.Height == 0) return;
        double nx = Math.Ceiling((double)area.Width / spacing.X), ny = Math.Ceiling((double)area.Height / spacing.Y);
        if (nx + ny + 2 > MaxDebugSegments) throw new ArgumentOutOfRangeException(nameof(spacing), "Grade excede o limite de linhas.");
        ValidateSegment(area.Position, new Vector2(area.Right, area.Bottom), thickness);
        for (int i = 0; i < (int)nx; i++)
        {
            float x = (float)(area.X + (double)i * spacing.X);
            DebugSegment(batch, new Vector2(x, area.Y), new Vector2(x, area.Bottom), color, thickness);
        }
        DebugSegment(batch, new Vector2(area.Right, area.Y), new Vector2(area.Right, area.Bottom), color, thickness);
        for (int i = 0; i < (int)ny; i++)
        {
            float y = (float)(area.Y + (double)i * spacing.Y);
            DebugSegment(batch, new Vector2(area.X, y), new Vector2(area.Right, y), color, thickness);
        }
        DebugSegment(batch, new Vector2(area.X, area.Bottom), new Vector2(area.Right, area.Bottom), color, thickness);
    }

    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static bool Finite(Matrix3x2 value) => float.IsFinite(value.M11) && float.IsFinite(value.M12) && float.IsFinite(value.M21) && float.IsFinite(value.M22) && float.IsFinite(value.M31) && float.IsFinite(value.M32);
    private static void ValidateBatchAndThickness(SpriteBatch batch, float thickness)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!float.IsFinite(thickness) || thickness <= 0) throw new ArgumentOutOfRangeException(nameof(thickness));
    }
    private static void ValidateSegment(Vector2 from, Vector2 to, float thickness)
    {
        if (!Finite(from) || !Finite(to)) throw new ArgumentException("Pontos devem ser finitos.");
        double dx = (double)to.X - from.X, dy = (double)to.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        // SpriteBatch calcula os vértices em float; a margem conserva os intermediários finitos.
        double magnitude = Math.Max(Math.Max(Math.Abs((double)from.X), Math.Abs((double)from.Y)), Math.Max(Math.Abs((double)to.X), Math.Abs((double)to.Y)));
        if (magnitude + length + thickness > float.MaxValue) throw new OverflowException("Geometria não representável com segurança em float.");
    }
    private static void DebugSegment(SpriteBatch batch, Vector2 from, Vector2 to, Color color, float thickness)
    {
        double dx = (double)to.X - from.X, dy = (double)to.Y - from.Y;
        float length = (float)Math.Sqrt(dx * dx + dy * dy);
        if (length == 0) return;
        batch.Draw(batch.GraphicsDevice.WhiteTexture, new RectangleF(from.X, from.Y - thickness * 0.5f, length, thickness), null, color, (float)Math.Atan2(dy, dx), new Vector2(0, 0.5f));
    }
}
