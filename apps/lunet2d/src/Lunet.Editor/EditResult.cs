namespace Lunet.Editor;

/// <summary>Uma edição de texto com a seleção resultante (<see cref="SelectionStart"/> ≤ <see cref="SelectionEnd"/>).</summary>
public readonly record struct EditResult(int Start, int DeleteLength, string Insert, int SelectionStart, int SelectionEnd)
{
    public string Apply(string text) => string.Concat(text.AsSpan(0, Start), Insert, text.AsSpan(Start + DeleteLength));
}
