using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Urbe.Core.Assistant;

namespace Urbe.Client;

public sealed partial class AssistantPanel
{
    public static string ModelPrice(AiModelInfo? model) => model?.Price is { } price
        ? "US$ " + price.Input.ToString("0.######", CultureInfo.InvariantCulture) + " entrada · US$ " +
            price.Output.ToString("0.######", CultureInfo.InvariantCulture) + " saída / 1M tokens"
        : "Preço indisponível · não significa gratuito";

    private string ModelPriceFor(AiModelChoice? choice)
    {
        var providerId = choice?.ProviderId ?? Provider()?.Id;
        var model = choice?.Model ?? ModelId();
        return ModelPrice(providerId is not null && _cfg.ModelCache.TryGetValue(providerId, out var cache)
            ? cache.List.FirstOrDefault(m => m.Id == model) : null);
    }

    private string UsageSummary()
    {
        var u = _conv.Usage;
        if (u.Input + u.Output == 0) return "Custos e consumo";
        var money = u.Cost is { } cost ? "US$ " + cost.ToString("0.0000", CultureInfo.InvariantCulture) + " informado*"
            : "custo indisponível";
        return FmtTokens(u.Input + u.Output) + " acumulados · " + money;
    }

    private void ShowUsage()
    {
        var body = OpenView("Custos e consumo");
        body.Children.Add(Wrap(UsageSummary(), 15, UrbeTheme.Text));
        body.Children.Add(Hint("Tokens acumulados somam as chamadas da conversa, inclusive reenvios de contexto. Não representam a janela ocupada da próxima chamada."));
        body.Children.Add(Hint("*Valores informados somam somente a cobrança retornada pelos provedores. O total pode ser parcial: chamadas sem custo informado e geração de imagens não são contabilizadas como gratuitas."));
        body.Children.Add(Section("Tarifa do modelo selecionado", Wrap(ModelPriceFor(null), 13, UrbeTheme.Text2),
            Hint("Fonte: catálogo do provedor salvo no aparelho; consulta renovada ao selecionar modelos. Cache e outras unidades só podem ser discriminados quando o provedor os informa.")));
        foreach (var message in _conv.Messages.Where(m => m.Role == "assistant" && m.Meta?.Usage is not null))
        {
            var u = message.Meta!.Usage!;
            var model = message.Meta.Model ?? "modelo não informado";
            var usage = FmtTokens(u.Input) + " entrada · " + FmtTokens(u.Output) + " saída";
            var cost = u.Cost is { } value ? "US$ " + value.ToString("0.000000", CultureInfo.InvariantCulture) + " informado pelo provedor"
                : "Custo não informado";
            body.Children.Add(Section(model, Wrap(usage, 12, UrbeTheme.Text2), Wrap(cost, 12, UrbeTheme.Text2)));
        }
        body.Children.Add(Hint("Não há um teto financeiro obrigatório. Consulte a fatura do provedor para conciliação."));
    }
}
