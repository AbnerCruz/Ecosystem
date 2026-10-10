namespace Lunet.UI;

/// <summary>Distribui células de grade em colunas adaptativas para inventários e menus mobile.</summary>
/// <remarks>Geometria pura: não desenha, não captura dedos e não cria controles.
/// Combine com GraphicsDevice.SafeArea, TouchButton.Bounds e TouchScrollArea.
/// O conteúdo pode exceder a altura do viewport e deve ser recortado pelo jogo.</remarks>
/// <example><code>
/// var grid = new Lunet.UI.UiGridLayout(80, 48, spacing: 8, padding: 12);
/// var cells = new RectangleF[20];
/// grid.Arrange(device.SafeArea, cells.Length, cells);
/// </code></example>
public readonly struct UiGridLayout
{
    /// <summary>Cria uma grade com colunas automáticas e altura de célula fixa.</summary>
    /// <param name="minimumCellWidth">Largura mínima positiva desejada; cede em telas menores.</param>
    /// <param name="cellHeight">Altura positiva das células em coordenadas virtuais.</param>
    /// <param name="spacing">Intervalo não negativo entre células nos dois eixos.</param>
    /// <param name="padding">Margem não negativa; na horizontal colapsa se a tela for estreita.</param>
    /// <param name="maxColumns">Máximo de colunas positivo; zero significa sem limite.</param>
    public UiGridLayout(float minimumCellWidth, float cellHeight,
        float spacing = 0, float padding = 0, int maxColumns = 0)
    {
        if (!float.IsFinite(minimumCellWidth) || minimumCellWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumCellWidth));
        if (!float.IsFinite(cellHeight) || cellHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellHeight));
        if (!float.IsFinite(spacing) || spacing < 0)
            throw new ArgumentOutOfRangeException(nameof(spacing));
        if (!float.IsFinite(padding) || padding < 0)
            throw new ArgumentOutOfRangeException(nameof(padding));
        if (maxColumns < 0) throw new ArgumentOutOfRangeException(nameof(maxColumns));
        MinimumCellWidth = minimumCellWidth;
        CellHeight = cellHeight;
        Spacing = spacing;
        Padding = padding;
        MaxColumns = maxColumns;
    }

    /// <summary>Largura preferida de cada célula; não impede o colapso em telas estreitas.</summary>
    public float MinimumCellWidth { get; }
    /// <summary>Altura de cada célula, independente do número de colunas.</summary>
    public float CellHeight { get; }
    /// <summary>Espaçamento comum entre linhas e colunas.</summary>
    public float Spacing { get; }
    /// <summary>Margem do container; só a margem horizontal se reduz em viewport estreito.</summary>
    public float Padding { get; }
    /// <summary>Limite opcional de colunas, ou zero para sem limite.</summary>
    public int MaxColumns { get; }

    /// <summary>Calcula o número de colunas para o espaço horizontal disponível.</summary>
    /// <param name="area">Retângulo finito de tamanho não negativo.</param>
    /// <param name="itemCount">Quantidade não negativa de células.</param>
    /// <returns>Zero quando não há itens; ao menos uma coluna para itens existentes.</returns>
    public int GetColumnCount(RectangleF area, int itemCount)
    {
        Validate(area, itemCount);
        if (itemCount == 0) return 0;
        double inset = Math.Min(Padding, (double)area.Width / 2d);
        double available = Math.Max(0d, area.Width - 2d * inset);
        double columns = Math.Floor((available + Spacing) / (MinimumCellWidth + Spacing));
        int limit = MaxColumns == 0 ? itemCount : Math.Min(itemCount, MaxColumns);
        return (int)Math.Clamp(columns, 1d, limit);
    }

    /// <summary>Obtém a altura total necessária para uma área rolável.</summary>
    /// <param name="area">Área cujo tamanho horizontal determina as colunas.</param>
    /// <param name="itemCount">Quantidade não negativa de células.</param>
    /// <returns>Altura do conteúdo incluindo padding superior/inferior, ou zero para vazio.</returns>
    /// <remarks>Use em TouchScrollArea.ContentHeight após qualquer mudança de largura.
    /// Lança OverflowException para alturas fora da faixa de float.</remarks>
    public float GetContentHeight(RectangleF area, int itemCount)
    {
        int columns = GetColumnCount(area, itemCount);
        if (columns == 0) return 0;
        int rows = (itemCount - 1) / columns + 1;
        return ToFloat((double)Padding * 2d + rows * (double)CellHeight + (rows - 1d) * Spacing);
    }

    /// <summary>Preenche retângulos na ordem linha/coluna sem alocar memória gerenciada.</summary>
    /// <param name="area">Retângulo de referência no mesmo espaço dos toques.</param>
    /// <param name="itemCount">Quantidade de células a distribuir.</param>
    /// <param name="results">Buffer pré-alocado com espaço para todos os itens; sobra preservada.</param>
    /// <returns>Quantidade de colunas usadas; zero se não houver itens.</returns>
    /// <remarks>Usa linhas de altura fixa, e colunas igualmente largas. A largura mínima cede
    /// quando não cabe nem uma célula. Não há clipping vertical nem criação de arrays.
    /// Limites inválidos ou falta de buffer são rejeitados antes de escrever resultados.</remarks>
    public int Arrange(RectangleF area, int itemCount, Span<RectangleF> results)
    {
        int columns = GetColumnCount(area, itemCount);
        if (results.Length < itemCount)
            throw new ArgumentException("Buffer de saída menor que itemCount.", nameof(results));
        if (columns == 0) return 0;
        int rows = (itemCount - 1) / columns + 1;
        double inset = Math.Min(Padding, (double)area.Width / 2d);
        double innerWidth = Math.Max(0d, area.Width - 2d * inset);
        double cellWidth = Math.Max(0d, (innerWidth - (columns - 1d) * Spacing) / columns);
        double strideX = cellWidth + Spacing;
        double strideY = CellHeight + (double)Spacing;
        double x0 = area.X + inset;
        double y0 = area.Y + (double)Padding;

        // Pré-validação de toda a grade: erro de coordenada não deixa saída parcial.
        float width = ToFloat(cellWidth);
        float height = CellHeight;
        ToFloat(x0); ToFloat(y0);
        ToFloat(x0 + (columns - 1d) * strideX + cellWidth);
        ToFloat(y0 + (rows - 1d) * strideY + CellHeight);

        for (int i = 0; i < itemCount; i++)
        {
            int row = i / columns;
            int column = i % columns;
            results[i] = new RectangleF(ToFloat(x0 + column * strideX),
                ToFloat(y0 + row * strideY), width, height);
        }
        return columns;
    }

    private static void Validate(RectangleF area, int itemCount)
    {
        if (itemCount < 0) throw new ArgumentOutOfRangeException(nameof(itemCount));
        if (!float.IsFinite(area.X) || !float.IsFinite(area.Y)
            || !float.IsFinite(area.Width) || !float.IsFinite(area.Height)
            || area.Width < 0 || area.Height < 0
            || !float.IsFinite(area.Right) || !float.IsFinite(area.Bottom))
            throw new ArgumentOutOfRangeException(nameof(area));
    }

    private static float ToFloat(double value)
    {
        if (!double.IsFinite(value) || value < -float.MaxValue || value > float.MaxValue)
            throw new OverflowException("Grade fora do intervalo de coordenadas float.");
        return (float)value;
    }
}
