using System.Numerics;

namespace Lunet;

/// <summary>Posição, rotação (radianos), escala e origem (ponto de pivô, em coordenadas locais).</summary>
public struct Transform2D
{
    /// <summary>Posição no mundo.</summary>
    public Vector2 Position;
    /// <summary>Rotação em radianos.</summary>
    public float Rotation;
    /// <summary>Escala em cada eixo.</summary>
    public Vector2 Scale;
    /// <summary>Pivô da rotação e da escala, em coordenadas locais.</summary>
    public Vector2 Origin;

    /// <summary>Cria uma transformação.</summary>
    /// <param name="position">Posição.</param>
    /// <param name="rotation">Rotação em radianos.</param>
    /// <param name="scale">Escala</param>
    /// <param name="origin">Pivô em coordenadas locais.</param>
    public Transform2D(Vector2 position, float rotation = 0f, Vector2? scale = null, Vector2 origin = default)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale ?? Vector2.One;
        Origin = origin;
    }

    /// <summary>Transformação neutra: sem movimento, rotação ou escala.</summary>
    public static Transform2D Identity => new(Vector2.Zero);

    /// <summary>Matriz local→mundo: mover a origem para (0,0), escalar, girar, posicionar.</summary>
    public readonly Matrix3x2 ToMatrix() =>
        Matrix3x2.CreateTranslation(-Origin) * Matrix3x2.CreateScale(Scale) * Matrix3x2.CreateRotation(Rotation) * Matrix3x2.CreateTranslation(Position);

    /// <summary>Converte um ponto local para o mundo.</summary>
    /// <param name="local">Ponto em coordenadas locais.</param>
    /// <returns>Ponto em coordenadas do mundo.</returns>
    public readonly Vector2 TransformPoint(Vector2 local) => Vector2.Transform(local, ToMatrix());

    /// <summary>Converte um ponto do mundo para o espaço local. Falha se a escala for zero.</summary>
    public readonly bool TryInverseTransformPoint(Vector2 world, out Vector2 local)
    {
        if (Matrix3x2.Invert(ToMatrix(), out var inverse)) { local = Vector2.Transform(world, inverse); return true; }
        local = default;
        return false;
    }

    /// <summary>Direção "para frente" (eixo X local) no mundo.</summary>
    public readonly Vector2 Forward => new(MathF.Cos(Rotation), MathF.Sin(Rotation));
}
