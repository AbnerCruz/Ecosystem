namespace Lunet.Editor;

/// <summary>Substituição de <see cref="DeleteLength"/> caracteres a partir de <see cref="Start"/> por <see cref="Insert"/>.</summary>
public readonly record struct TextEdit(int Start, int DeleteLength, string Insert, int CaretAfter)
{
    public string Apply(string text) => string.Concat(text.AsSpan(0, Start), Insert, text.AsSpan(Start + DeleteLength));
}
