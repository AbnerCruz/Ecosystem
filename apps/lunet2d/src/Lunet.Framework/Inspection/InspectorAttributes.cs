namespace Lunet;

/// <summary>Limita um número a um intervalo no Inspector (vira um controle deslizante).</summary>
/// <param name="min">Menor valor permitido.</param>
/// <param name="max">Maior valor permitido.</param>
/// <example>
/// <code>
/// public sealed class Options
/// {
///     [Range(0, 1)] public float Volume = 0.8f;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class RangeAttribute(float min, float max) : Attribute
{
    /// <summary>Menor valor permitido.</summary>
    public float Min { get; } = min;
    /// <summary>Maior valor permitido.</summary>
    public float Max { get; } = max;
}

/// <summary>O Inspector mostra o valor, mas não deixa editar.</summary>
/// <example>
/// <code>
/// public sealed class Stats
/// {
///     [ReadOnly] public int Frame;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ReadOnlyAttribute : Attribute
{
}

/// <summary>Esconde o campo ou propriedade do Inspector.</summary>
/// <example>
/// <code>
/// public sealed class Player
/// {
///     public int Lives = 3;
///     [Hidden] public int InternalCounter;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class HiddenAttribute : Attribute
{
}

/// <summary>Mostra um campo privado no Inspector (por padrão só os públicos aparecem).</summary>
/// <example>
/// <code>
/// public sealed class Boss
/// {
///     [Inspect] private int _phase = 1;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class InspectAttribute : Attribute
{
}

/// <summary>Texto de várias linhas no Inspector.</summary>
/// <param name="lines">Quantidade de linhas visíveis.</param>
/// <example>
/// <code>
/// public sealed class Dialogue
/// {
///     [Multiline(4)] public string Text = "Olá!\nBem-vindo.";
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class MultilineAttribute(int lines = 4) : Attribute
{
    /// <summary>Quantidade de linhas visíveis.</summary>
    public int Lines { get; } = lines;
}

/// <summary>Trata um texto como cor hexadecimal (<c>#RRGGBB</c> ou <c>#RRGGBBAA</c>) e mostra um seletor de cor.</summary>
/// <example>
/// <code>
/// public sealed class Theme
/// {
///     [Color] public string Background = "#1E2230";
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ColorAttribute : Attribute
{
}

/// <summary>Trata um texto como caminho de arquivo do projeto e mostra um seletor de arquivo.</summary>
/// <param name="filter">Extensões aceitas, por exemplo <c>.png;.jpg</c>. Vazio aceita qualquer arquivo.</param>
/// <example>
/// <code>
/// public sealed class Level
/// {
///     [File(".json")] public string DataFile = "Data/level1.json";
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class FileAttribute(string filter = "") : Attribute
{
    /// <summary>Extensões aceitas.</summary>
    public string Filter { get; } = filter;
}

/// <summary>Tipos de recurso do projeto, usados por <see cref="AssetAttribute"/>.</summary>
public enum AssetKind
{
    /// <summary>Qualquer recurso.</summary>
    Any,
    /// <summary>Imagem.</summary>
    Texture,
    /// <summary>Efeito sonoro.</summary>
    Sound,
    /// <summary>Música.</summary>
    Music,
    /// <summary>Dados em JSON ou texto.</summary>
    Data,
}

/// <summary>Trata um texto como o nome de um recurso do projeto (<c>Content/</c>) e mostra um seletor de recursos.</summary>
/// <param name="kind">Tipo de recurso aceito.</param>
/// <example>
/// <code>
/// public sealed class Enemy
/// {
///     [Asset(AssetKind.Sound)] public string HitSound = "hit.wav";
///     [Asset(AssetKind.Texture)] public string Sprite = "enemy.png";
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class AssetAttribute(AssetKind kind = AssetKind.Any) : Attribute
{
    /// <summary>Tipo de recurso aceito.</summary>
    public AssetKind Kind { get; } = kind;
}

/// <summary>Agrupa campos sob um título no Inspector.</summary>
/// <param name="name">Título do grupo.</param>
/// <example>
/// <code>
/// public sealed class Weapon
/// {
///     [Group("Combate")] public int Damage = 4;
///     [Group("Combate")] public float Cooldown = 0.5f;
///     [Group("Visual")] public string Sprite = "sword.png";
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class GroupAttribute(string name) : Attribute
{
    /// <summary>Título do grupo.</summary>
    public string Name { get; } = name;
}

/// <summary>Texto de ajuda mostrado junto ao campo no Inspector.</summary>
/// <param name="text">Explicação curta.</param>
/// <example>
/// <code>
/// public sealed class Ship
/// {
///     [Tooltip("Pixels por segundo")] public float Speed = 220;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class TooltipAttribute(string text) : Attribute
{
    /// <summary>Explicação curta.</summary>
    public string Text { get; } = text;
}
