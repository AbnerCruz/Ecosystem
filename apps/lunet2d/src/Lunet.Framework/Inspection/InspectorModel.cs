namespace Lunet;

/// <summary>Tipo de linha do Inspector.</summary>
public enum InspectorItemKind
{
    /// <summary>Título de seção.</summary>
    Header,
    /// <summary>Texto informativo.</summary>
    Label,
    /// <summary>Linha divisória.</summary>
    Separator,
    /// <summary>Botão que executa uma ação no jogo.</summary>
    Button,
    /// <summary>Valor que pode ser lido e (se permitido) editado.</summary>
    Field,
}

/// <summary>Forma de edição de um campo.</summary>
public enum InspectorFieldKind
{
    /// <summary>Número inteiro.</summary>
    Int,
    /// <summary>Número decimal.</summary>
    Float,
    /// <summary>Verdadeiro ou falso.</summary>
    Bool,
    /// <summary>Texto.</summary>
    Text,
    /// <summary>Valor de uma enumeração.</summary>
    Enum,
    /// <summary>Ponto ou vetor de dois números.</summary>
    Vector2,
    /// <summary>Cor (<c>Lunet.Graphics.Color</c> ou texto hexadecimal).</summary>
    Color,
    /// <summary>Tipo sem editor: só mostra o valor como texto.</summary>
    Other,
}

/// <summary>Uma linha do Inspector, independente de interface: quem desenha (o app) lê e grava por <see cref="Getter"/> e <see cref="Setter"/>.</summary>
public sealed class InspectorItem
{
    /// <summary>Tipo da linha.</summary>
    public InspectorItemKind Kind { get; init; }
    /// <summary>Rótulo ou texto da linha.</summary>
    public string Label { get; init; } = "";
    /// <summary>Forma de edição, para linhas do tipo <see cref="InspectorItemKind.Field"/>.</summary>
    public InspectorFieldKind FieldKind { get; init; }
    /// <summary>Tipo do valor.</summary>
    public Type? ValueType { get; init; }
    /// <summary>Lê o valor atual do jogo.</summary>
    public Func<object?>? Getter { get; init; }
    /// <summary>Grava um valor; nulo quando o campo é somente leitura. Chame na thread do jogo (<c>Game.Dispatcher</c>).</summary>
    public Action<object?>? Setter { get; init; }
    /// <summary>Menor valor (controle deslizante).</summary>
    public float? Min { get; init; }
    /// <summary>Maior valor (controle deslizante).</summary>
    public float? Max { get; init; }
    /// <summary>Linhas de um texto multilinha; zero quando é uma linha só.</summary>
    public int MultilineLines { get; init; }
    /// <summary>Texto de ajuda.</summary>
    public string? Tooltip { get; init; }
    /// <summary>Filtro de extensões, quando o campo é um arquivo.</summary>
    public string? FileFilter { get; init; }
    /// <summary>Tipo de recurso, quando o campo é um recurso do projeto.</summary>
    public AssetKind? Asset { get; init; }
    /// <summary>Nomes dos valores, quando é uma enumeração.</summary>
    public IReadOnlyList<string>? EnumNames { get; init; }
    /// <summary>Profundidade de aninhamento (para recuo).</summary>
    public int Depth { get; init; }
    /// <summary>Ação de um botão.</summary>
    public Action? Action { get; init; }

    /// <summary>Verdadeiro se o campo pode ser editado.</summary>
    public bool IsEditable => Kind == InspectorItemKind.Field && Setter is not null;
}

/// <summary>
/// Onde um <see cref="Inspector{T}"/> descreve a interface do objeto. Cada chamada adiciona uma linha; o app desenha as linhas.
/// </summary>
public sealed class InspectorContext
{
    private readonly List<InspectorItem> _items = [];

    /// <summary>Linhas adicionadas até agora.</summary>
    public IReadOnlyList<InspectorItem> Items => _items;

    /// <summary>Recuo atual; aumenta dentro de <see cref="Indent"/>.</summary>
    public int Depth { get; private set; }

    /// <summary>Adiciona um título de seção.</summary>
    /// <param name="text">Texto do título.</param>
    public void Header(string text) => _items.Add(new InspectorItem { Kind = InspectorItemKind.Header, Label = text, Depth = Depth });

    /// <summary>Adiciona um texto informativo.</summary>
    /// <param name="text">Texto a mostrar.</param>
    public void Label(string text) => _items.Add(new InspectorItem { Kind = InspectorItemKind.Label, Label = text, Depth = Depth });

    /// <summary>Adiciona uma linha divisória.</summary>
    public void Separator() => _items.Add(new InspectorItem { Kind = InspectorItemKind.Separator, Depth = Depth });

    /// <summary>Adiciona um botão que executa <paramref name="action"/> na thread do jogo.</summary>
    /// <param name="label">Texto do botão.</param>
    /// <param name="action">O que fazer ao tocar.</param>
    public void Button(string label, Action action) =>
        _items.Add(new InspectorItem { Kind = InspectorItemKind.Button, Label = label, Action = action, Depth = Depth });

    /// <summary>Adiciona um campo editável de qualquer tipo suportado (int, float, bool, string, enum, Vector2, Color).</summary>
    /// <typeparam name="TValue">Tipo do valor.</typeparam>
    /// <param name="label">Nome do campo.</param>
    /// <param name="get">Lê o valor atual.</param>
    /// <param name="set">Grava o valor; nulo deixa o campo somente leitura.</param>
    /// <param name="tooltip">Texto de ajuda opcional.</param>
    public void Field<TValue>(string label, Func<TValue> get, Action<TValue>? set = null, string? tooltip = null) =>
        _items.Add(new InspectorItem
        {
            Kind = InspectorItemKind.Field,
            Label = label,
            FieldKind = InspectorFieldKindOf(typeof(TValue)),
            ValueType = typeof(TValue),
            Getter = () => get(),
            Setter = set is null ? null : v => set((TValue)v!),
            EnumNames = typeof(TValue).IsEnum ? Enum.GetNames(typeof(TValue)) : null,
            Tooltip = tooltip,
            Depth = Depth,
        });

    /// <summary>Adiciona um controle deslizante para um número decimal.</summary>
    /// <param name="label">Nome do campo.</param>
    /// <param name="get">Lê o valor atual.</param>
    /// <param name="set">Grava o valor.</param>
    /// <param name="min">Menor valor.</param>
    /// <param name="max">Maior valor.</param>
    public void Slider(string label, Func<float> get, Action<float> set, float min, float max) =>
        _items.Add(new InspectorItem
        {
            Kind = InspectorItemKind.Field,
            Label = label,
            FieldKind = InspectorFieldKind.Float,
            ValueType = typeof(float),
            Getter = () => get(),
            Setter = v => set(Math.Clamp((float)v!, min, max)),
            Min = min,
            Max = max,
            Depth = Depth,
        });

    /// <summary>Aumenta o recuo das linhas adicionadas por <paramref name="content"/>.</summary>
    /// <param name="content">Adiciona as linhas recuadas.</param>
    public void Indent(Action content)
    {
        Depth++;
        try { content(); }
        finally { Depth--; }
    }

    /// <summary>Adiciona uma linha já montada (usado pelo Inspector automático).</summary>
    /// <param name="item">Linha a adicionar.</param>
    public void Add(InspectorItem item) => _items.Add(item);

    /// <summary>Forma de edição usada para um tipo.</summary>
    /// <param name="type">Tipo do valor.</param>
    /// <returns>A forma de edição, ou <see cref="InspectorFieldKind.Other"/> se o tipo não tem editor.</returns>
    public static InspectorFieldKind InspectorFieldKindOf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(bool)) return InspectorFieldKind.Bool;
        if (type == typeof(string)) return InspectorFieldKind.Text;
        if (type.IsEnum) return InspectorFieldKind.Enum;
        if (type == typeof(System.Numerics.Vector2)) return InspectorFieldKind.Vector2;
        if (type == typeof(Graphics.Color)) return InspectorFieldKind.Color;
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return InspectorFieldKind.Float;
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(uint) ||
            type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte)) return InspectorFieldKind.Int;
        return InspectorFieldKind.Other;
    }
}

/// <summary>
/// Inspector customizado para um tipo do jogo. Crie uma classe que herda de <c>Inspector&lt;MeuTipo&gt;</c> no projeto e o
/// Inspector do app a usa no lugar da lista automática de campos.
/// </summary>
/// <typeparam name="T">Tipo inspecionado.</typeparam>
public abstract class Inspector<T> : IInspector where T : class
{
    /// <summary>Descreve a interface do objeto adicionando linhas em <paramref name="ui"/>.</summary>
    /// <param name="ui">Onde adicionar linhas.</param>
    /// <param name="target">Objeto inspecionado.</param>
    public abstract void OnInspect(InspectorContext ui, T target);

    Type IInspector.TargetType => typeof(T);

    void IInspector.Inspect(InspectorContext ui, object target) => OnInspect(ui, (T)target);
}

/// <summary>Contrato interno dos inspectors customizados; use <see cref="Inspector{T}"/>.</summary>
public interface IInspector
{
    /// <summary>Tipo que este inspector sabe mostrar.</summary>
    Type TargetType { get; }

    /// <summary>Adiciona as linhas do objeto.</summary>
    /// <param name="ui">Onde adicionar linhas.</param>
    /// <param name="target">Objeto inspecionado.</param>
    void Inspect(InspectorContext ui, object target);
}
