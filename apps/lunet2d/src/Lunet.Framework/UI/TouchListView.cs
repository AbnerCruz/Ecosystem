using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.UI;

/// <summary>Lista vertical de seleção por toque, com linhas virtuais e rolagem por inércia.</summary>
/// <example><code>
/// var list = new Lunet.UI.TouchListView(new RectangleF(0, 0, 200, 120), 8, 40);
/// list.Update(input, (float)time.DeltaSeconds);
/// </code></example>
/// <remarks>Não guarda os itens, não consome input de outros controles e não altera arquivos de jogos.
/// Um toque breve ativa a linha; arrastar rola sem ativá-la. Atualize mesmo desabilitada.
/// O desenho abre seus próprios lotes SpriteBatch com clipping; chame Draw fora de Begin/End.
/// Nenhuma alocação gerenciada no Update, hit-test ou consulta de linhas visíveis.</remarks>
public sealed class TouchListView
{
    private readonly TouchScrollArea _scroll;
    private readonly float _rowHeight;
    private readonly float _spacing;
    private readonly int[] _previousDownIds = new int[InputState.MaxTouches];
    private int _previousCount;
    private int _count;
    private int _touchId;
    private int _pressedIndex = -1;
    private int _selectedIndex = -1;
    private Vector2 _pressPosition;
    private bool _captured;
    private bool _enabled = true;

    /// <summary>Cria lista com altura uniforme e espaçamento em coordenadas virtuais.</summary>
    /// <param name="bounds">Viewport finita com largura e altura não negativas.</param>
    /// <param name="itemCount">Quantidade de linhas, inclusive zero.</param>
    /// <param name="rowHeight">Altura positiva finita de uma linha.</param>
    /// <param name="spacing">Espaço vertical finito não negativo entre linhas.</param>
    public TouchListView(RectangleF bounds, int itemCount, float rowHeight, float spacing = 0)
    {
        if (!float.IsFinite(rowHeight) || rowHeight <= 0) throw new ArgumentOutOfRangeException(nameof(rowHeight));
        if (!float.IsFinite(spacing) || spacing < 0) throw new ArgumentOutOfRangeException(nameof(spacing));
        float height = CalculateHeight(itemCount, rowHeight, spacing);
        _rowHeight = rowHeight;
        _spacing = spacing;
        _count = itemCount;
        _scroll = new TouchScrollArea(bounds, height);
    }

    /// <summary>Área do viewport. Mudanças de layout cancelam gesto sem limpar seleção.</summary>
    public RectangleF Bounds
    {
        get => _scroll.Bounds;
        set
        {
            if (_scroll.Bounds == value) return;
            _scroll.Bounds = value;
            Cancel();
        }
    }

    /// <summary>Quantidade de linhas. Alterações limitam scroll e invalidam seleção fora da lista.</summary>
    public int ItemCount
    {
        get => _count;
        set
        {
            var height = CalculateHeight(value, _rowHeight, _spacing);
            _scroll.ContentHeight = height;
            _count = value;
            if (_selectedIndex >= value) _selectedIndex = -1;
            Cancel();
        }
    }

    /// <summary>Altura uniforme das linhas.</summary>
    public float RowHeight => _rowHeight;
    /// <summary>Espaço entre linhas, que não responde a toques.</summary>
    public float Spacing => _spacing;
    /// <summary>Deslocamento desde o início, limitado ao conteúdo.</summary>
    public float OffsetY => _scroll.OffsetY;
    /// <summary>Maior deslocamento vertical possível.</summary>
    public float MaximumOffsetY => _scroll.MaximumOffsetY;

    /// <summary>Habilita/desabilita a interação sem perder seleção ou posição.</summary>
    public bool IsEnabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            _scroll.IsEnabled = value;
            if (!value) Cancel();
        }
    }

    /// <summary>Há um dedo reservado para um gesto da lista.</summary>
    public bool IsCaptured => _captured;
    /// <summary>A área subjacente está sendo arrastada pelo dedo capturado.</summary>
    public bool IsDragging => _scroll.IsDragging;
    /// <summary>Índice selecionado, ou -1 se nenhum.</summary>
    public int SelectedIndex => _selectedIndex;
    /// <summary>Índice ativado no último Update, ou -1. Uma ativação pode repetir a seleção atual.</summary>
    public int ActivatedIndex { get; private set; } = -1;
    /// <summary>Verdadeiro apenas no Update que mudou a seleção por toque.</summary>
    public bool WasSelectionChanged { get; private set; }

    /// <summary>Altera a seleção por código, sem emitir pulsos de ativação.</summary>
    /// <param name="index">Índice da seleção, ou -1 para limpar.</param>
    public void Select(int index)
    {
        if (index < -1 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
        _selectedIndex = index;
    }

    /// <summary>Posiciona a lista dentro dos limites, cancelando momentum.</summary>
    /// <param name="offsetY">Distância finita desejada a partir do topo.</param>
    public void ScrollTo(float offsetY) => _scroll.ScrollTo(offsetY);

    /// <summary>Torna uma linha visível, opcionalmente centralizada.</summary>
    /// <param name="index">Índice da linha.</param>
    /// <param name="center">Centraliza a linha quando verdadeiro.</param>
    public void ScrollToItem(int index, bool center = false)
    {
        ValidateIndex(index);
        double top = (double)index * Pitch;
        double target = _scroll.OffsetY;
        if (center) target = top + _rowHeight / 2d - Bounds.Height / 2d;
        else if (top < target) target = top;
        else if (top + _rowHeight > target + Bounds.Height) target = top + _rowHeight - Bounds.Height;
        _scroll.ScrollTo((float)Math.Clamp(target, 0d, _scroll.MaximumOffsetY));
    }

    /// <summary>Retorna o retângulo virtual de uma linha, deslocado pelo scroll.</summary>
    /// <param name="index">Índice a consultar.</param>
    /// <returns>Retângulo virtual deslocado pelo scroll.</returns>
    public RectangleF GetItemBounds(int index)
    {
        ValidateIndex(index);
        double y = (double)Bounds.Y + index * Pitch - OffsetY;
        if (y < -float.MaxValue || y > float.MaxValue) throw new OverflowException("Linha fora do espaço representável.");
        return new RectangleF(Bounds.X, (float)y, Bounds.Width, _rowHeight);
    }

    /// <summary>Retorna a linha sob um ponto, ou -1 fora do viewport, sobre intervalo ou área vazia.</summary>
    /// <param name="position">Coordenadas virtuais do toque.</param>
    /// <returns>Índice atingido, ou -1 se não houver linha.</returns>
    public int HitTest(Vector2 position)
    {
        if (_count == 0 || !Bounds.Contains(position)) return -1;
        double local = (double)position.Y - Bounds.Y + OffsetY;
        double item = Math.Floor(local / Pitch);
        if (item < 0 || item >= _count) return -1;
        return local - item * Pitch < _rowHeight ? (int)item : -1;
    }

    /// <summary>Intervalo [first, endExclusive) de linhas que podem intersectar o viewport.</summary>
    /// <param name="first">Primeiro índice potencialmente visível.</param>
    /// <param name="endExclusive">Índice final exclusivo.</param>
    public void GetVisibleRange(out int first, out int endExclusive)
    {
        if (_count == 0 || Bounds.Width == 0 || Bounds.Height == 0)
        {
            first = 0; endExclusive = 0; return;
        }
        double start = Math.Floor(OffsetY / Pitch);
        double end = Math.Ceiling(((double)OffsetY + Bounds.Height) / Pitch);
        first = (int)Math.Clamp(start, 0d, _count);
        endExclusive = (int)Math.Clamp(end, first, _count);
    }

    /// <summary>Geometria proporcional da barra, vazia se não há overflow.</summary>
    /// <param name="width">Largura solicitada da barra.</param>
    /// <param name="minimumHeight">Altura mínima da barra.</param>
    /// <returns>Retângulo da barra ou valor default sem overflow.</returns>
    public RectangleF GetThumbBounds(float width = 5, float minimumHeight = 24) =>
        _scroll.GetThumbBounds(width, minimumHeight);

    /// <summary>Cancela o gesto atual e momentum sem alterar a seleção.</summary>
    public void Cancel()
    {
        _captured = false;
        _pressedIndex = -1;
        _scroll.Cancel();
        ActivatedIndex = -1;
        WasSelectionChanged = false;
    }

    /// <summary>Atualiza seleção e arraste uma vez por passo fixo, preservando o ID de dedo original.</summary>
    /// <param name="input">Snapshot de toque em coordenadas virtuais.</param>
    /// <param name="deltaSeconds">Delta finito não negativo do passo, em segundos.</param>
    public void Update(InputState input, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(input);
        ActivatedIndex = -1;
        WasSelectionChanged = false;
        _scroll.Update(input, deltaSeconds);

        if (!_enabled || _count == 0 || Bounds.Width == 0 || Bounds.Height == 0)
            CancelTap();
        else if (_captured)
        {
            if (!input.TouchCollection.TryGetById(_touchId, out var touch)
                || !float.IsFinite(touch.Position.X) || !float.IsFinite(touch.Position.Y))
                CancelTap();
            else if (touch.Phase == TouchPhase.Released)
            {
                int index = HitTest(touch.Position);
                if (index >= 0 && index == _pressedIndex
                    && !_scroll.WasDragged
                    && Vector2.DistanceSquared(_pressPosition, touch.Position) < 36f)
                {
                    ActivatedIndex = index;
                    WasSelectionChanged = _selectedIndex != index;
                    _selectedIndex = index;
                }
                CancelTap();
            }
            else if (touch.Phase is TouchPhase.Pressed or TouchPhase.Moved)
            {
                if (_scroll.IsDragging || Vector2.DistanceSquared(_pressPosition, touch.Position) >= 36f)
                    _pressedIndex = -1;
            }
            else CancelTap();
        }
        else
        {
            foreach (var touch in input.Touches)
            {
                if (touch.Phase != TouchPhase.Pressed || WasDown(touch.Id)
                    || !_scroll.Bounds.Contains(touch.Position))
                    continue;
                _captured = true;
                _touchId = touch.Id;
                _pressPosition = touch.Position;
                _pressedIndex = HitTest(touch.Position);
                break;
            }
        }

        _previousCount = 0;
        foreach (var touch in input.Touches)
            if (touch.IsDown) _previousDownIds[_previousCount++] = touch.Id;
    }

    /// <summary>Desenha apenas linhas visíveis e a barra, recortadas na área, com dados fornecidos pelo jogo.</summary>
    /// <remarks>Inicia e encerra os próprios lotes, portanto não pode ser chamado entre Begin/End.
    /// Não cria ou libera fonte/textura. O chamador controla o conteúdo e a paleta.</remarks>
    /// <param name="batch">Lote gráfico próprio do jogo.</param>
    /// <param name="font">Fonte de propriedade do jogo.</param>
    /// <param name="labels">Rótulos de cada linha, sem rótulos nulos.</param>
    /// <param name="style">Paleta de fundo e primeiro plano.</param>
    /// <param name="selectedBackground">Cor da linha selecionada e da barra.</param>
    /// <param name="textScale">Escala positiva finita do texto.</param>
    public void Draw(SpriteBatch batch, SpriteFont font, IReadOnlyList<string> labels,
        TouchButtonStyle style, Color selectedBackground, float textScale = 1)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(labels);
        if (labels.Count < _count) throw new ArgumentException("Quantidade de rótulos menor que ItemCount.", nameof(labels));
        if (!float.IsFinite(textScale) || textScale <= 0) throw new ArgumentOutOfRangeException(nameof(textScale));
        if (Bounds.Width == 0 || Bounds.Height == 0) return;

        GetVisibleRange(out int first, out int end);
        batch.Begin(clip: Bounds);
        try
        {
            for (int i = first; i < end; i++)
            {
                RectangleF rect = GetItemBounds(i);
                var color = !_enabled ? style.DisabledBackground
                    : _captured && i == _pressedIndex ? style.PressedBackground
                    : i == _selectedIndex ? selectedBackground : style.Background;
                batch.FillRect(rect, color);
                string label = labels[i] ?? throw new ArgumentException("Rótulo nulo.", nameof(labels));
                Vector2 measure = font.Measure(label, textScale);
                if (!float.IsFinite(measure.X) || !float.IsFinite(measure.Y))
                    throw new OverflowException("Texto fora do espaço representável.");
                batch.DrawString(font, label, new Vector2(rect.X + 10, rect.Center.Y - measure.Y / 2),
                    _enabled ? style.Foreground : style.DisabledForeground, textScale);
            }
        }
        finally { batch.End(); }

        var thumb = GetThumbBounds();
        if (thumb.Width > 0)
        {
            batch.Begin();
            try { batch.FillRect(thumb, selectedBackground); }
            finally { batch.End(); }
        }
    }

    private double Pitch => (double)_rowHeight + _spacing;

    private void CancelTap() { _captured = false; _pressedIndex = -1; }

    private bool WasDown(int id)
    {
        for (int i = 0; i < _previousCount; i++)
            if (_previousDownIds[i] == id) return true;
        return false;
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static float CalculateHeight(int count, float rowHeight, float spacing)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        double size = count == 0 ? 0d : (double)count * rowHeight + (double)(count - 1) * spacing;
        if (!double.IsFinite(size) || size > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(count), "Conteúdo excede coordenadas representáveis.");
        return (float)size;
    }
}
