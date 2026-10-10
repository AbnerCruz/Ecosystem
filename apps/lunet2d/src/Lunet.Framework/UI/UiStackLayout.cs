using System.Numerics;

namespace Lunet.UI;

/// <summary>Alinhamento das linhas ou colunas na direção transversal da pilha.</summary>
public enum UiStackAlignment
{
    /// <summary>Ocupa toda a extensão transversal disponível.</summary>
    Stretch,
    /// <summary>Alinha ao início da extensão transversal.</summary>
    Start,
    /// <summary>Centraliza na extensão transversal.</summary>
    Center,
    /// <summary>Alinha ao final da extensão transversal.</summary>
    End
}

/// <summary>Uma linha/coluna de tamanho fixo ou flexível em um layout de pilha.</summary>
/// <example><code>
/// var rows = new[] { UiStackItem.Fixed(48), UiStackItem.Flex(), UiStackItem.Fixed(48) };
/// </code></example>
/// <remarks>Não guarda controle ou texto. Tamanho transversal zero significa usar todo o espaço disponível.
/// default equivale a Fixed(0). Imutável.</remarks>
public readonly struct UiStackItem
{
    private UiStackItem(float extent, float crossAxisSize, bool flexible)
    {
        Extent = extent; CrossAxisSize = crossAxisSize; IsFlexible = flexible;
    }

    /// <summary>Tamanho fixo na direção principal ou peso, se flexível.</summary>
    public float Extent { get; }
    /// <summary>Tamanho transversal preferido, ou zero para preencher.</summary>
    public float CrossAxisSize { get; }
    /// <summary>Se verdadeiro, Extent representa peso proporcional no espaço restante.</summary>
    public bool IsFlexible { get; }

    /// <summary>Cria item com tamanho principal fixo não negativo.</summary>
    /// <param name="size">Extensão fixa positiva ou zero, em coordenadas virtuais.</param>
    /// <param name="crossAxisSize">Extensão transversal opcional; zero preenche.</param>
    /// <returns>Uma descrição imutável para Arrange.</returns>
    public static UiStackItem Fixed(float size, float crossAxisSize = 0)
    {
        ValidateNonnegative(size, nameof(size));
        ValidateNonnegative(crossAxisSize, nameof(crossAxisSize));
        return new(size, crossAxisSize, false);
    }

    /// <summary>Cria item que recebe parte proporcional do espaço livre.</summary>
    /// <param name="weight">Peso positivo finito relativo aos demais itens flexíveis.</param>
    /// <param name="crossAxisSize">Extensão transversal opcional; zero preenche.</param>
    /// <returns>Uma descrição imutável para Arrange.</returns>
    public static UiStackItem Flex(float weight = 1, float crossAxisSize = 0)
    {
        if (!float.IsFinite(weight) || weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));
        ValidateNonnegative(crossAxisSize, nameof(crossAxisSize));
        return new(weight, crossAxisSize, true);
    }

    private static void ValidateNonnegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }
}

/// <summary>Distribui controles em coluna ou linha responsiva, sem alocar memória a cada layout.</summary>
/// <example><code>
/// var layout = UiStackLayout.Vertical(spacing: 8, padding: 12);
/// var specs = new[] { UiStackItem.Fixed(48), UiStackItem.Flex(), UiStackItem.Fixed(48) };
/// var rectangles = new RectangleF[specs.Length];
/// layout.Arrange(graphics.SafeArea, specs, rectangles);
/// </code></example>
/// <remarks>Compatível com LayoutRect (use GetBounds como área), SafeArea e Bounds dos controles por toque.
/// Itens fixos maiores que a área não são reduzidos: excedem o viewport e devem ser recortados ao desenhar.
/// Itens flexíveis recebem zero quando não há espaço. Margens são limitadas a metade da área,
/// e o espaçamento não é reduzido. Não cria uma árvore de UI nem consome eventos.</remarks>
public readonly struct UiStackLayout
{
    private UiStackLayout(bool horizontal, float spacing, float padding, UiStackAlignment alignment)
    {
        CheckNonnegative(spacing, nameof(spacing));
        CheckNonnegative(padding, nameof(padding));
        if (!Enum.IsDefined(alignment)) throw new ArgumentOutOfRangeException(nameof(alignment));
        IsHorizontal = horizontal; Spacing = spacing; Padding = padding; Alignment = alignment;
    }

    /// <summary>Se verdadeiro, itens são posicionados da esquerda para a direita.</summary>
    public bool IsHorizontal { get; }
    /// <summary>Espaço não negativo entre itens consecutivos.</summary>
    public float Spacing { get; }
    /// <summary>Margem uniforme interna nas duas direções; colapsa em áreas muito pequenas.</summary>
    public float Padding { get; }
    /// <summary>Regra transversal de alinhamento.</summary>
    public UiStackAlignment Alignment { get; }

    /// <summary>Cria pilha vertical de cima para baixo.</summary>
    /// <param name="spacing">Espaço finito não negativo entre itens.</param>
    /// <param name="padding">Margem interna finita não negativa.</param>
    /// <param name="alignment">Alinhamento transversal dos itens.</param>
    /// <returns>Layout vertical imutável.</returns>
    public static UiStackLayout Vertical(float spacing = 0, float padding = 0,
        UiStackAlignment alignment = UiStackAlignment.Stretch) => new(false, spacing, padding, alignment);

    /// <summary>Cria pilha horizontal da esquerda para a direita.</summary>
    /// <param name="spacing">Espaço finito não negativo entre itens.</param>
    /// <param name="padding">Margem interna finita não negativa.</param>
    /// <param name="alignment">Alinhamento transversal dos itens.</param>
    /// <returns>Layout horizontal imutável.</returns>
    public static UiStackLayout Horizontal(float spacing = 0, float padding = 0,
        UiStackAlignment alignment = UiStackAlignment.Stretch) => new(true, spacing, padding, alignment);

    /// <summary>Calcula retângulos no espaço do pai usando somente spans preexistentes.</summary>
    /// <param name="area">Viewport ou Bounds do painel, com limites finitos e tamanho não negativo.</param>
    /// <param name="items">Descrições dos itens, por ordem de exibição.</param>
    /// <param name="results">Buffer de saída com capacidade para todos os itens; excedente não é alterado.</param>
    /// <remarks>O cálculo é determinístico e sem alocação. Tamanho flexível é distribuído por peso depois
    /// de reservar margens, espaçamento e itens fixos. Saídas fora do intervalo float geram OverflowException.</remarks>
    public void Arrange(RectangleF area, ReadOnlySpan<UiStackItem> items, Span<RectangleF> results)
    {
        if (results.Length < items.Length) throw new ArgumentException("Buffer de saída pequeno demais.", nameof(results));
        if (!float.IsFinite(area.X) || !float.IsFinite(area.Y) ||
            !float.IsFinite(area.Width) || !float.IsFinite(area.Height) ||
            area.Width < 0 || area.Height < 0 ||
            !float.IsFinite(area.Right) || !float.IsFinite(area.Bottom))
            throw new ArgumentOutOfRangeException(nameof(area));

        double axisLength = IsHorizontal ? area.Width : area.Height;
        double crossLength = IsHorizontal ? area.Height : area.Width;
        double axisPadding = Math.Min(Padding, axisLength / 2d);
        double crossPadding = Math.Min(Padding, crossLength / 2d);
        double innerCross = Math.Max(0d, crossLength - 2d * crossPadding);
        double fixedTotal = 0d, flexTotal = 0d;
        foreach (ref readonly var item in items)
        {
            if (!float.IsFinite(item.Extent) || item.Extent < 0 ||
                !float.IsFinite(item.CrossAxisSize) || item.CrossAxisSize < 0 ||
                (item.IsFlexible && item.Extent <= 0))
                throw new ArgumentException("Item inválido.", nameof(items));
            if (item.IsFlexible) flexTotal += item.Extent; else fixedTotal += item.Extent;
        }
        double gaps = items.Length < 2 ? 0 : (double)Spacing * (items.Length - 1);
        double remaining = Math.Max(0d, axisLength - 2d * axisPadding - fixedTotal - gaps);
        double axis = (IsHorizontal ? area.X : area.Y) + axisPadding;
        double crossStart = (IsHorizontal ? area.Y : area.X) + crossPadding;

        for (int i = 0; i < items.Length; i++)
        {
            ref readonly var item = ref items[i];
            double length = item.IsFlexible ? (flexTotal > 0d ? remaining * item.Extent / flexTotal : 0d) : item.Extent;
            double crossSize = Alignment == UiStackAlignment.Stretch || item.CrossAxisSize == 0
                ? innerCross : Math.Min(innerCross, item.CrossAxisSize);
            double extraCross = innerCross - crossSize;
            double offset = Alignment switch
            {
                UiStackAlignment.Center => extraCross / 2d,
                UiStackAlignment.End => extraCross,
                _ => 0d
            };
            var rect = IsHorizontal
                ? new RectangleF(ToFloat(axis), ToFloat(crossStart + offset), ToFloat(length), ToFloat(crossSize))
                : new RectangleF(ToFloat(crossStart + offset), ToFloat(axis), ToFloat(crossSize), ToFloat(length));
            if (!float.IsFinite(rect.Right) || !float.IsFinite(rect.Bottom))
                throw new OverflowException("Retângulo calculado fora do intervalo float.");
            results[i] = rect;
            axis += length + Spacing;
        }
    }

    private static float ToFloat(double value)
    {
        if (!double.IsFinite(value) || value < -float.MaxValue || value > float.MaxValue)
            throw new OverflowException("Layout fora do intervalo float.");
        return (float)value;
    }

    private static void CheckNonnegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }
}
