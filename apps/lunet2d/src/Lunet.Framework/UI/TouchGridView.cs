using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.UI;

/// <summary>Inventário em grade por toque com seleção, rolagem por inércia e desenho virtualizado.</summary>
/// <example><code>
/// var grid = new Lunet.UI.TouchGridView(new RectangleF(12, 80, 320, 380), 30,
///     new Lunet.UI.UiGridLayout(80, 52, spacing: 8, padding: 12));
/// grid.Update(input, (float)time.DeltaSeconds);
/// if (grid.ActivatedIndex >= 0) log.Info("Item ativado");
/// </code></example>
/// <remarks>Composição de UiGridLayout e TouchScrollArea: não armazena itens nem cria
/// controles por célula. Captura toque por ID; soltar na mesma célula ativa somente se
/// não houve arraste. Não consome eventos de controles vizinhos; evite áreas sobrepostas.
/// Desenho com SpriteBatch.Begin/End próprios e clipping; chame Draw fora de Begin/End.
/// Sem alocações gerenciadas no Update, HitTest, GetVisibleRange ou GetItemBounds.</remarks>
public sealed class TouchGridView
{
    private readonly UiGridLayout _layout;
    private readonly TouchScrollArea _scroll;
    private readonly int[] _previousDownIds = new int[InputState.MaxTouches];
    private int _previousCount;
    private int _count;
    private int _touchId;
    private int _pressedIndex = -1;
    private int _selectedIndex = -1;
    private Vector2 _pressPosition;
    private bool _captured;
    private bool _enabled = true;

    /// <summary>Cria inventário com tamanho, total e configuração de grade.</summary>
    /// <param name="bounds">Viewport finito, sem tamanhos negativos.</param>
    /// <param name="itemCount">Quantidade de itens não negativa.</param>
    /// <param name="layout">Configuração válida de colunas e alturas.</param>
    public TouchGridView(RectangleF bounds, int itemCount, UiGridLayout layout)
    {
        float height = layout.GetContentHeight(bounds, itemCount);
        _layout = layout;
        _scroll = new TouchScrollArea(bounds, height);
        _count = itemCount;
    }

    /// <summary>Retângulo visível. Redimensionar recalcula colunas e cancela gestos e inércia.</summary>
    public RectangleF Bounds
    {
        get => _scroll.Bounds;
        set
        {
            if (value == _scroll.Bounds) return;
            float height = _layout.GetContentHeight(value, _count);
            _scroll.Bounds = value;
            _scroll.ContentHeight = height;
            Cancel();
        }
    }

    /// <summary>Total de itens. Alterar reajusta o scroll e invalida seleção fora do intervalo.</summary>
    public int ItemCount
    {
        get => _count;
        set
        {
            if (value == _count) return;
            float height = _layout.GetContentHeight(Bounds, value);
            _scroll.ContentHeight = height;
            _count = value;
            if (_selectedIndex >= _count) _selectedIndex = -1;
            Cancel();
        }
    }

    /// <summary>Layout imutável da grade.</summary>
    public UiGridLayout Layout => _layout;
    /// <summary>Deslocamento vertical a partir do topo do conteúdo.</summary>
    public float OffsetY => _scroll.OffsetY;
    /// <summary>Maior offset vertical possível.</summary>
    public float MaximumOffsetY => _scroll.MaximumOffsetY;
    /// <summary>O dedo da grade está capturado.</summary>
    public bool IsCaptured => _captured;
    /// <summary>O toque está arrastando o conteúdo.</summary>
    public bool IsDragging => _scroll.IsDragging;
    /// <summary>Índice selecionado, ou -1.</summary>
    public int SelectedIndex => _selectedIndex;
    /// <summary>Índice ativado ao soltar neste Update, ou -1.</summary>
    public int ActivatedIndex { get; private set; } = -1;
    /// <summary>Verdadeiro no Update que mudou a seleção por toque.</summary>
    public bool WasSelectionChanged { get; private set; }

    /// <summary>Controla a interação; desabilitar cancela gesto e inércia.</summary>
    public bool IsEnabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            _scroll.IsEnabled = value;
            if (!value) Cancel();
        }
    }

    /// <summary>Altera seleção programaticamente sem emitir clique.</summary>
    /// <param name="index">Índice válido, ou -1 para limpar seleção.</param>
    public void Select(int index)
    {
        if (index < -1 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
        _selectedIndex = index;
    }

    /// <summary>Posiciona a rolagem, limitando-a ao conteúdo.</summary>
    /// <param name="offsetY">Deslocamento finito desejado.</param>
    public void ScrollTo(float offsetY) => _scroll.ScrollTo(offsetY);

    /// <summary>Garante visibilidade da célula; opcionalmente centraliza na altura.</summary>
    /// <param name="index">Índice da célula.</param>
    /// <param name="center">Centraliza quando verdadeiro.</param>
    public void ScrollToItem(int index, bool center = false)
    {
        RectangleF cell = _layout.GetCellBounds(Bounds, _count, index);
        double top = (double)cell.Y - Bounds.Y;
        double target = _scroll.OffsetY;
        if (center) target = top + cell.Height / 2d - Bounds.Height / 2d;
        else if (top < target) target = top;
        else if (top + cell.Height > target + Bounds.Height) target = top + cell.Height - Bounds.Height;
        _scroll.ScrollTo((float)Math.Clamp(target, 0d, _scroll.MaximumOffsetY));
    }

    /// <summary>Retorna a geometria da célula no viewport, descontada a rolagem.</summary>
    /// <param name="index">Índice existente.</param>
    /// <returns>Retângulo virtual, possivelmente fora do viewport.</returns>
    public RectangleF GetItemBounds(int index)
    {
        RectangleF cell = _layout.GetCellBounds(Bounds, _count, index);
        double y = (double)cell.Y - OffsetY;
        if (!double.IsFinite(y) || y < -float.MaxValue || y > float.MaxValue)
            throw new OverflowException("Célula fora do espaço representável.");
        return new RectangleF(cell.X, (float)y, cell.Width, cell.Height);
    }

    /// <summary>Encontra a célula sob o toque, ou -1 para espaços vazios e margens.</summary>
    /// <param name="position">Coordenadas virtuais do dedo.</param>
    /// <returns>Índice atingido, ou -1.</returns>
    public int HitTest(Vector2 position) => _layout.HitTest(Bounds, _count, OffsetY, position);

    /// <summary>Consulta índices que podem ser desenhados, em O(1) sem alocação.</summary>
    /// <param name="first">Primeiro índice inclusivo.</param>
    /// <param name="endExclusive">Limite exclusivo.</param>
    public void GetVisibleRange(out int first, out int endExclusive)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            first = endExclusive = 0;
            return;
        }
        _layout.GetVisibleRange(Bounds, _count, OffsetY, out first, out endExclusive);
    }

    /// <summary>Obtém a barra de rolagem, vazia sem overflow.</summary>
    /// <param name="width">Largura positiva desejada.</param>
    /// <param name="minimumHeight">Altura mínima desejada.</param>
    /// <returns>Retângulo para desenhar acima do conteúdo.</returns>
    public RectangleF GetThumbBounds(float width = 5, float minimumHeight = 24)
        => _scroll.GetThumbBounds(width, minimumHeight);

    /// <summary>Cancela captura e inércia sem limpar seleção ou posição.</summary>
    public void Cancel()
    {
        CancelTap();
        _scroll.Cancel();
        ActivatedIndex = -1;
        WasSelectionChanged = false;
    }

    /// <summary>Atualiza toque, rolagem e seleção por dedo e passo fixo.</summary>
    /// <param name="input">Snapshot de entrada.</param>
    /// <param name="deltaSeconds">Delta finito não negativo do passo.</param>
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
                if (index >= 0 && index == _pressedIndex && !_scroll.WasDragged
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
                    || !_scroll.Bounds.Contains(touch.Position)) continue;
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

    /// <summary>Desenha apenas células visíveis, texto e indicador de rolagem.</summary>
    /// <param name="batch">Lote gráfico do jogo, fora de um Begin/End ativo.</param>
    /// <param name="font">Fonte de propriedade do jogo.</param>
    /// <param name="labels">Rótulos fornecidos pelo jogo para todos os itens, sem nulos.</param>
    /// <param name="style">Estilo de fundo e texto.</param>
    /// <param name="selectedBackground">Cor da célula selecionada e da barra.</param>
    /// <param name="textScale">Escala positiva finita da fonte.</param>
    /// <remarks>O jogo pode desenhar suas próprias imagens e ícones usando GetVisibleRange
    /// e GetItemBounds, sem precisar de labels ou criar sprites por item.</remarks>
    public void Draw(SpriteBatch batch, SpriteFont font, IReadOnlyList<string> labels,
        TouchButtonStyle style, Color selectedBackground, float textScale = 1)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(labels);
        if (labels.Count < _count) throw new ArgumentException("Rótulos insuficientes.", nameof(labels));
        if (!float.IsFinite(textScale) || textScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(textScale));
        if (Bounds.Width == 0 || Bounds.Height == 0) return;
        GetVisibleRange(out int first, out int end);
        batch.Begin(clip: Bounds);
        try
        {
            for (int i = first; i < end; i++)
            {
                RectangleF rect = GetItemBounds(i);
                Color background = !_enabled ? style.DisabledBackground
                    : _captured && _pressedIndex == i ? style.PressedBackground
                    : _selectedIndex == i ? selectedBackground : style.Background;
                batch.FillRect(rect, background);
                string label = labels[i] ?? throw new ArgumentException("Rótulo nulo.", nameof(labels));
                Vector2 measure = font.Measure(label, textScale);
                if (!float.IsFinite(measure.X) || !float.IsFinite(measure.Y))
                    throw new OverflowException("Texto fora do espaço representável.");
                batch.DrawString(font, label, new Vector2(rect.Center.X - measure.X / 2,
                    rect.Center.Y - measure.Y / 2), _enabled ? style.Foreground : style.DisabledForeground, textScale);
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

    private void CancelTap() { _captured = false; _pressedIndex = -1; }

    private bool WasDown(int id)
    {
        for (int i = 0; i < _previousCount; i++)
            if (_previousDownIds[i] == id) return true;
        return false;
    }
}
