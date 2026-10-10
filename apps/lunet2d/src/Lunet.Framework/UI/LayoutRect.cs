using System.Numerics;

namespace Lunet.UI;

/// <summary>Layout de um retângulo por âncoras normalizadas e offsets no espaço do pai.</summary>
/// <example><code>
/// var panel = Lunet.UI.LayoutRect.Stretch(12, 12, 12, 12);
/// var bounds = panel.GetBounds(new RectangleF(0, 0, 360, 640));
/// var button = Lunet.UI.LayoutRect.Fixed(Vector2.One, new Vector2(100, 44), Vector2.One, new Vector2(-8, -8));
/// var buttonBounds = button.GetBounds(bounds);
/// </code></example>
/// <remarks>Imutável e sem alocação por cálculo. Retorna geometria para desenhar e testar toque;
/// não possui filhos, eventos, texto, recorte ou temas. default é um retângulo zero no canto superior esquerdo do pai.</remarks>
public readonly struct LayoutRect
{
    /// <summary>Define as âncoras dos limites e seus deslocamentos em unidades do pai.</summary>
    /// <param name="anchorMin">Âncora superior/esquerda em 0–1, inclusivo.</param>
    /// <param name="anchorMax">Âncora inferior/direita em 0–1, maior ou igual a anchorMin por eixo.</param>
    /// <param name="offsetMin">Deslocamento finito do limite superior/esquerdo.</param>
    /// <param name="offsetMax">Deslocamento finito do limite inferior/direito.</param>
    public LayoutRect(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        CheckUnit(anchorMin, nameof(anchorMin)); CheckUnit(anchorMax, nameof(anchorMax));
        if (anchorMax.X < anchorMin.X || anchorMax.Y < anchorMin.Y)
            throw new ArgumentException("anchorMax deve ser maior ou igual a anchorMin.", nameof(anchorMax));
        CheckFinite(offsetMin, nameof(offsetMin)); CheckFinite(offsetMax, nameof(offsetMax));
        AnchorMin = anchorMin; AnchorMax = anchorMax; OffsetMin = offsetMin; OffsetMax = offsetMax;
    }

    /// <summary>Fração do pai em que começa o retângulo.</summary>
    public Vector2 AnchorMin { get; }
    /// <summary>Fração do pai em que termina o retângulo.</summary>
    public Vector2 AnchorMax { get; }
    /// <summary>Deslocamento dos limites esquerdo/superior.</summary>
    public Vector2 OffsetMin { get; }
    /// <summary>Deslocamento dos limites direito/inferior.</summary>
    public Vector2 OffsetMax { get; }

    /// <summary>Cria um retângulo de tamanho fixo preso a um ponto do pai.</summary>
    /// <param name="anchor">Ponto normalizado 0–1 do pai (0,0 = topo/esquerda; 1,1 = base/direita).</param>
    /// <param name="size">Tamanho finito não negativo em unidades do pai.</param>
    /// <param name="pivot">Ponto normalizado 0–1 do retângulo preso à âncora; padrão canto superior esquerdo.</param>
    /// <param name="offset">Deslocamento finito do ponto de ancoragem.</param>
    /// <returns>Layout fixo; lança OverflowException se seus offsets não couberem em float.</returns>
    public static LayoutRect Fixed(Vector2 anchor, Vector2 size, Vector2 pivot = default, Vector2 offset = default)
    {
        CheckUnit(anchor, nameof(anchor)); CheckUnit(pivot, nameof(pivot));
        CheckFinite(size, nameof(size)); CheckFinite(offset, nameof(offset));
        if (size.X < 0 || size.Y < 0) throw new ArgumentOutOfRangeException(nameof(size));
        double x = offset.X - (double)size.X * pivot.X, y = offset.Y - (double)size.Y * pivot.Y;
        return new(anchor, anchor, new(ToFloat(x), ToFloat(y)), new(ToFloat(x + size.X), ToFloat(y + size.Y)));
    }

    /// <summary>Ocupa o pai com margens internas em cada lado.</summary>
    /// <param name="left">Margem esquerda finita não negativa.</param>
    /// <param name="top">Margem superior finita não negativa.</param>
    /// <param name="right">Margem direita finita não negativa.</param>
    /// <param name="bottom">Margem inferior finita não negativa.</param>
    /// <returns>Layout esticado; margens grandes podem colapsar seu tamanho a zero.</returns>
    public static LayoutRect Stretch(float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        CheckMargin(left, nameof(left)); CheckMargin(top, nameof(top));
        CheckMargin(right, nameof(right)); CheckMargin(bottom, nameof(bottom));
        return new(Vector2.Zero, Vector2.One, new(left, top), new(-right, -bottom));
    }

    /// <summary>Calcula os limites no mesmo espaço de coordenadas do pai.</summary>
    /// <param name="parent">Retângulo finito com tamanho não negativo e limites representáveis em float.</param>
    /// <returns>Retângulo para desenho/Contains; lança OverflowException quando o resultado não cabe em float.</returns>
    /// <remarks>Cada limite é posição do pai + tamanho × âncora + offset. Limites invertidos colapsam
    /// a zero no limite mínimo, sem tamanho negativo. Offsets podem colocar o layout fora do pai;
    /// não há clipping automático. Para aninhar, passe o resultado como pai do próximo layout.</remarks>
    public RectangleF GetBounds(RectangleF parent)
    {
        if (!float.IsFinite(parent.X) || !float.IsFinite(parent.Y)
            || !float.IsFinite(parent.Width) || !float.IsFinite(parent.Height)
            || parent.Width < 0 || parent.Height < 0
            || !float.IsFinite(parent.Right) || !float.IsFinite(parent.Bottom))
            throw new ArgumentOutOfRangeException(nameof(parent));
        double left = parent.X + (double)parent.Width * AnchorMin.X + OffsetMin.X;
        double top = parent.Y + (double)parent.Height * AnchorMin.Y + OffsetMin.Y;
        double right = parent.X + (double)parent.Width * AnchorMax.X + OffsetMax.X;
        double bottom = parent.Y + (double)parent.Height * AnchorMax.Y + OffsetMax.Y;
        right = Math.Max(left, right); bottom = Math.Max(top, bottom);
        // Confira também os limites: largura finita sozinha não impede X + Width = Inf.
        ToFloat(right); ToFloat(bottom);
        var result = new RectangleF(ToFloat(left), ToFloat(top), ToFloat(right - left), ToFloat(bottom - top));
        if (!float.IsFinite(result.Right) || !float.IsFinite(result.Bottom))
            throw new OverflowException("O layout não cabe em coordenadas float.");
        return result;
    }

    private static float ToFloat(double value)
    {
        if (value < -float.MaxValue || value > float.MaxValue) throw new OverflowException("O layout não cabe em coordenadas float.");
        return (float)value;
    }
    private static void CheckFinite(Vector2 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) throw new ArgumentOutOfRangeException(name);
    }
    private static void CheckUnit(Vector2 value, string name)
    {
        CheckFinite(value, name);
        if (value.X < 0 || value.X > 1 || value.Y < 0 || value.Y > 1) throw new ArgumentOutOfRangeException(name);
    }
    private static void CheckMargin(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }
}
