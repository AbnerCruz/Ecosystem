using System.Globalization;

namespace Lunet.Core;

/// <summary>Busca acessível para comandos da IDE, independente do Android e do editor de código.</summary>
public static class WorkbenchCommands
{
    private static readonly CompareInfo Compare = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;

    /// <summary>Filtra títulos sem distinguir caixa ou acentos, mantendo a ordem original.</summary>
    public static IReadOnlyList<string> Filter(IReadOnlyList<string> labels, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return labels.ToArray();
        var terms = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return labels.Where(label => terms.All(term =>
            Compare.IndexOf(label, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)).ToArray();
    }
}
