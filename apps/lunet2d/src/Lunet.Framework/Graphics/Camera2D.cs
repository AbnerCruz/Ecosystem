using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Câmera 2D: posição no centro da vista, zoom e rotação, sem alterar coordenadas do jogo.</summary>
/// <remarks>A vista usa coordenadas virtuais, não pixels físicos. O toque de Input já está nesse espaço.
/// Use GraphicsDevice.SurfaceToVirtual antes de converter um ponto físico. A câmera é opcional;
/// um lote sem câmera continua desenhando diretamente na vista.</remarks>
/// <example>
/// <code>
/// var camera = new Camera2D { Position = position, Zoom = 2 };
/// if (input.TryGetPointer(out var touch))
///     position = camera.ScreenToWorld(touch, device.ViewSize);
/// batch.Begin(camera);
/// batch.Draw(texture, position, Color.White);
/// batch.End();
/// </code>
/// </example>
public sealed class Camera2D
{
    private Vector2 _position;
    private float _zoom = 1;
    private float _rotation;

    /// <summary>Ponto do mundo mostrado no centro da vista. Padrão: origem do mundo.</summary>
    public Vector2 Position
    {
        get => _position;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
                throw new ArgumentOutOfRangeException(nameof(value), "A posição deve ser finita.");
            _position = value;
        }
    }

    /// <summary>Escala uniforme; 2 duplica o tamanho aparente. Positiva, finita e com inverso finito; padrão 1.</summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            if (!float.IsFinite(value) || value <= 0 || !float.IsFinite(1f / value))
                throw new ArgumentOutOfRangeException(nameof(value), "O zoom e seu inverso devem ser positivos e finitos.");
            _zoom = value;
        }
    }

    /// <summary>Rotação da câmera em radianos, finita; o mundo gira no sentido inverso. Padrão zero.</summary>
    public float Rotation
    {
        get => _rotation;
        set
        {
            if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "A rotação deve ser finita.");
            _rotation = value;
        }
    }

    /// <summary>Matriz mundo→vista: deslocamento, rotação inversa, zoom e centralização.</summary>
    /// <param name="viewSize">Tamanho positivo e finito da vista virtual ou do render target.</param>
    /// <returns>Matriz para desenhar pontos do mundo na vista; sem alocação.</returns>
    public Matrix3x2 GetViewMatrix(Vector2 viewSize)
    {
        ValidateViewSize(viewSize);
        return Matrix3x2.CreateTranslation(-_position)
            * Matrix3x2.CreateRotation(-_rotation)
            * Matrix3x2.CreateScale(_zoom)
            * Matrix3x2.CreateTranslation(viewSize * 0.5f);
    }

    /// <summary>Obtém uma AABB conservadora no mundo que contém toda a vista, mesmo com rotação.</summary>
    /// <remarks>Use com TileMap.Draw para recorte de tiles. A rotação pode incluir tiles extras nos cantos,
    /// mas não deve descartar tiles visíveis. Usa coordenadas do mundo e tamanho virtual ou do render target.
    /// A operação não aloca e rejeita extensões não representáveis por RectangleF finito.</remarks>
    /// <param name="viewSize">Tamanho positivo e finito da vista virtual ou do render target.</param>
    /// <returns>Retângulo conservador da vista em coordenadas de mundo, sem alocação.</returns>
    public RectangleF GetWorldViewBounds(Vector2 viewSize)
    {
        ValidateViewSize(viewSize);
        var topLeft = ScreenToWorld(Vector2.Zero, viewSize);
        var topRight = ScreenToWorld(new Vector2(viewSize.X, 0), viewSize);
        var bottomLeft = ScreenToWorld(new Vector2(0, viewSize.Y), viewSize);
        var bottomRight = ScreenToWorld(viewSize, viewSize);

        var min = Vector2.Min(Vector2.Min(topLeft, topRight), Vector2.Min(bottomLeft, bottomRight));
        var max = Vector2.Max(Vector2.Max(topLeft, topRight), Vector2.Max(bottomLeft, bottomRight));
        if (!float.IsFinite(min.X) || !float.IsFinite(min.Y) ||
            !float.IsFinite(max.X) || !float.IsFinite(max.Y))
            throw new ArgumentOutOfRangeException(nameof(viewSize), "A vista excede as coordenadas representáveis.");

        // Arredondar para fora impede cortar tiles por perda de precisão nas bordas.
        float left = MathF.BitDecrement(min.X);
        float top = MathF.BitDecrement(min.Y);
        float right = MathF.BitIncrement(max.X);
        float bottom = MathF.BitIncrement(max.Y);
        float width = MathF.BitIncrement((float)((double)right - left));
        float height = MathF.BitIncrement((float)((double)bottom - top));
        if (!float.IsFinite(left) || !float.IsFinite(top) ||
            !float.IsFinite(width) || !float.IsFinite(height))
            throw new ArgumentOutOfRangeException(nameof(viewSize), "A AABB da vista excede RectangleF.");
        return new RectangleF(left, top, width, height);
    }

    /// <summary>Converte uma posição do mundo em coordenadas virtuais da vista.</summary>
    /// <param name="world">Posição no mundo.</param>
    /// <param name="viewSize">Tamanho positivo e finito da vista virtual ou do render target.</param>
    /// <returns>Ponto na vista, antes da escala física/letterbox.</returns>
    public Vector2 WorldToScreen(Vector2 world, Vector2 viewSize) => Vector2.Transform(world, GetViewMatrix(viewSize));

    /// <summary>Converte um ponto virtual da vista para o mundo, invertendo zoom e rotação.</summary>
    /// <param name="screen">Ponto virtual, por exemplo o toque fornecido por Input.</param>
    /// <param name="viewSize">Tamanho positivo e finito da vista virtual ou do render target.</param>
    /// <returns>Posição no mundo; não modifica o ponto nem a câmera.</returns>
    public Vector2 ScreenToWorld(Vector2 screen, Vector2 viewSize)
    {
        ValidateViewSize(viewSize);
        // Inversão analítica evita determinante próximo de zero com zoom pequeno.
        return Vector2.Transform((screen - viewSize * 0.5f) / _zoom, Matrix3x2.CreateRotation(_rotation)) + _position;
    }

    private static void ValidateViewSize(Vector2 viewSize)
    {
        if (!float.IsFinite(viewSize.X) || !float.IsFinite(viewSize.Y) || viewSize.X <= 0 || viewSize.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewSize), "O tamanho da vista deve ser positivo e finito.");
    }
}
