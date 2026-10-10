using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.UI;

/// <summary>Menu radial de acoes mobile; selecao arrastando e confirmacao ao soltar.</summary>
/// <remarks>O setor zero aponta para cima e os demais seguem no sentido horario. Um dedo
/// iniciado no centro pode arrastar ate a coroa. O jogo deve cancelar ao esconder ou pausar.
/// Nao consome input de controles vizinhos. Update e consultas nao alocam por quadro.</remarks>
/// <example><code>
/// var menu = new Lunet.UI.TouchRadialMenu(new Vector2(160, 240), 30, 100, 4);
/// menu.Update(input);
/// if (menu.ActivatedIndex >= 0) log.Info("Acao");
/// </code></example>
public sealed class TouchRadialMenu
{
    private readonly int[] _previousIds = new int[InputState.MaxTouches];
    private int _previousCount;
    private int _touchId;
    private int _itemCount;
    private bool _captured;
    private bool _enabled = true;
    private Vector2 _center;
    private float _inner;
    private float _outer;

    /// <summary>Cria o menu com geometria e quantidade de setores validas.</summary>
    /// <param name="center">Centro em coordenadas virtuais.</param>
    /// <param name="innerRadius">Zona morta, raio nao negativo.</param>
    /// <param name="outerRadius">Raio externo maior que o interno.</param>
    /// <param name="itemCount">Setores de 1 a 16.</param>
    public TouchRadialMenu(Vector2 center, float innerRadius, float outerRadius, int itemCount)
    {
        ValidateCount(itemCount);
        ValidateGeometry(center, innerRadius, outerRadius);
        _center = center; _inner = innerRadius; _outer = outerRadius; _itemCount = itemCount;
    }

    /// <summary>Centro atual do menu.</summary>
    public Vector2 Center => _center;
    /// <summary>Raio interno da coroa.</summary>
    public float InnerRadius => _inner;
    /// <summary>Raio externo da coroa.</summary>
    public float OuterRadius => _outer;
    /// <summary>Quantidade de setores; alteracoes cancelam um gesto.</summary>
    public int ItemCount
    {
        get => _itemCount;
        set
        {
            ValidateCount(value);
            if (value == _itemCount) return;
            _itemCount = value;
            Cancel();
        }
    }
    /// <summary>Permite ou bloqueia toques; desabilitar cancela o gesto.</summary>
    public bool IsEnabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (!value) Cancel();
        }
    }
    /// <summary>Existe um dedo capturado.</summary>
    public bool IsCaptured => _captured;
    /// <summary>Setor apontado atualmente, ou -1 fora da coroa.</summary>
    public int SelectedIndex { get; private set; } = -1;
    /// <summary>Indice confirmado neste Update apenas, ou -1.</summary>
    public int ActivatedIndex { get; private set; } = -1;

    /// <summary>Recalcula o menu sem cancelar toques quando a geometria permanece identica.</summary>
    /// <param name="center">Novo centro.</param>
    /// <param name="innerRadius">Novo raio interno.</param>
    /// <param name="outerRadius">Novo raio externo.</param>
    public void SetGeometry(Vector2 center, float innerRadius, float outerRadius)
    {
        ValidateGeometry(center, innerRadius, outerRadius);
        if (center == _center && innerRadius == _inner && outerRadius == _outer) return;
        _center = center; _inner = innerRadius; _outer = outerRadius;
        Cancel();
    }

    /// <summary>Encerra o gesto, mantendo os IDs acompanhados contra recaptura fantasma.</summary>
    public void Cancel()
    {
        _captured = false;
        SelectedIndex = -1;
        ActivatedIndex = -1;
    }

    /// <summary>Retorna o setor sob a posicao ou -1 na zona morta ou fora do anel.</summary>
    /// <param name="position">Toque em coordenadas virtuais.</param>
    /// <returns>Setor zero baseado no topo, sentido horario.</returns>
    public int HitTest(Vector2 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) return -1;
        double dx = (double)position.X - _center.X, dy = (double)position.Y - _center.Y;
        double distance2 = dx * dx + dy * dy;
        if (distance2 == 0 || distance2 < (double)_inner * _inner
            || distance2 > (double)_outer * _outer) return -1;
        double sector = 2 * Math.PI / _itemCount;
        double angle = (Math.Atan2(dy, dx) + Math.PI / 2 + sector / 2) % (2 * Math.PI);
        if (angle < 0) angle += 2 * Math.PI;
        return (int)(angle / sector) % _itemCount;
    }

    /// <summary>Ponto de referencia para icones e rotulos no meio de um setor.</summary>
    /// <param name="index">Setor existente.</param>
    /// <returns>Coordenadas virtuais do centro angular do setor.</returns>
    public Vector2 GetItemPosition(int index)
    {
        if ((uint)index >= (uint)_itemCount) throw new ArgumentOutOfRangeException(nameof(index));
        double angle = -Math.PI / 2 + 2 * Math.PI * index / _itemCount;
        double radius = ((double)_inner + _outer) / 2;
        return new((float)(_center.X + Math.Cos(angle) * radius),
            (float)(_center.Y + Math.Sin(angle) * radius));
    }

    /// <summary>Processa o dedo capturado; somente Released dentro da coroa confirma o setor.</summary>
    /// <param name="input">Snapshot dos dedos para o passo do jogo.</param>
    public void Update(InputState input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ActivatedIndex = -1;
        if (_enabled)
        {
            if (_captured)
            {
                if (!input.TouchCollection.TryGetById(_touchId, out var touch)) Cancel();
                else if (touch.Phase == TouchPhase.Released)
                {
                    ActivatedIndex = HitTest(touch.Position);
                    _captured = false;
                    SelectedIndex = -1;
                }
                else if (touch.Phase is TouchPhase.Pressed or TouchPhase.Moved)
                    SelectedIndex = HitTest(touch.Position);
                else Cancel();
            }
            else
            {
                foreach (var touch in input.Touches)
                {
                    if (touch.Phase != TouchPhase.Pressed || WasDown(touch.Id)
                        || !InsideOuter(touch.Position)) continue;
                    _touchId = touch.Id;
                    _captured = true;
                    SelectedIndex = HitTest(touch.Position);
                    break;
                }
            }
        }
        _previousCount = 0;
        foreach (var touch in input.Touches)
            if (touch.IsDown) _previousIds[_previousCount++] = touch.Id;
    }

    /// <summary>Desenha a coroa, divisoes, rotulos e arco do item apontado.</summary>
    /// <param name="batch">SpriteBatch ativo entre Begin e End.</param>
    /// <param name="font">Fonte administrada pelo jogo.</param>
    /// <param name="labels">Um rotulo nao nulo por setor.</param>
    /// <param name="border">Cor do contorno.</param>
    /// <param name="highlight">Cor do setor apontado.</param>
    /// <param name="foreground">Cor do texto.</param>
    /// <param name="textScale">Escala positiva do texto.</param>
    public void Draw(SpriteBatch batch, SpriteFont font, IReadOnlyList<string> labels,
        Color border, Color highlight, Color foreground, float textScale = 1f)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(labels);
        if (labels.Count != _itemCount) throw new ArgumentException("Rotulos e setores nao correspondem.", nameof(labels));
        if (!float.IsFinite(textScale) || textScale <= 0) throw new ArgumentOutOfRangeException(nameof(textScale));
        for (int i = 0; i < labels.Count; i++)
            if (labels[i] is null) throw new ArgumentException("Rotulo nulo.", nameof(labels));

        batch.Circle(_center, _outer, border, 2f);
        if (_inner > 0) batch.Circle(_center, _inner, border, 2f);
        double sector = 2 * Math.PI / _itemCount;
        for (int i = 0; i < _itemCount; i++)
        {
            double angle = -Math.PI / 2 + (i - 0.5) * sector;
            var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            batch.Line(_center + direction * _inner, _center + direction * _outer, border);
            Vector2 position = GetItemPosition(i) - font.Measure(labels[i], textScale) / 2;
            if (float.IsFinite(position.X) && float.IsFinite(position.Y))
                batch.DrawString(font, labels[i], position, foreground, textScale);
        }
        if (SelectedIndex < 0) return;
        double start = -Math.PI / 2 + (SelectedIndex - 0.5) * sector;
        Vector2 previous = ArcPoint(start);
        for (int i = 1; i <= 8; i++)
        {
            Vector2 next = ArcPoint(start + sector * i / 8);
            batch.Line(previous, next, highlight, 4f);
            previous = next;
        }
    }

    private Vector2 ArcPoint(double angle) =>
        new((float)(_center.X + Math.Cos(angle) * _outer), (float)(_center.Y + Math.Sin(angle) * _outer));

    private bool InsideOuter(Vector2 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) return false;
        double dx = (double)position.X - _center.X, dy = (double)position.Y - _center.Y;
        return dx * dx + dy * dy <= (double)_outer * _outer;
    }

    private bool WasDown(int id)
    {
        for (int i = 0; i < _previousCount; i++) if (_previousIds[i] == id) return true;
        return false;
    }

    private static void ValidateCount(int count)
    {
        if (count is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(count));
    }

    private static void ValidateGeometry(Vector2 center, float inner, float outer)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y)
            || !float.IsFinite(inner) || !float.IsFinite(outer)
            || inner < 0 || outer <= inner
            || Math.Abs((double)center.X) + outer > float.MaxValue
            || Math.Abs((double)center.Y) + outer > float.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(center), "Centro ou raios invalidos.");
    }
}
