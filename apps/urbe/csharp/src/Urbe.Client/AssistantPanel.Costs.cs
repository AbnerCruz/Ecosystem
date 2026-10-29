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
        var money = u.Cost is { } cost ? "US$ " + cost.ToString("0.0000", CultureInfo.InvariantCulture) +
            (u.CostSource == "partial" ? " parcial*" : u.CostSource == "estimated" ? " estimado*" : " informado*")
            : "custo desconhecido";
        return FmtTokens(u.Input + u.Output) + " acumulados · " + money;
    }

    private void ShowUsage()
    {
        var body = OpenView("Custos e consumo");
        body.Children.Add(Wrap(UsageSummary(), 15, UrbeTheme.Text));
        body.Children.Add(Hint("Tokens acumulados somam as chamadas da conversa, inclusive reenvios de contexto. Não representam a janela ocupada da próxima chamada."));
        body.Children.Add(Hint("*Cobranças ausentes nunca equivalem a zero. Totais mistos são parciais; tarifas de cache e de imagens podem não estar disponíveis."));
        body.Children.Add(Hint("Categorias de cache são subdivisões dos tokens de entrada e não entram novamente na soma."));
        body.Children.Add(Section("Tarifa do modelo selecionado", Wrap(ModelPriceFor(null), 13, UrbeTheme.Text2),
            Hint("Fonte: catálogo do provedor salvo no aparelho; consulta renovada ao selecionar modelos. Cache e outras unidades só podem ser discriminados quando o provedor os informa.")));
        foreach (var message in _conv.Messages.Where(m => m.Role == "assistant" && m.Meta?.Usage is not null))
        {
            var u = message.Meta!.Usage!;
            var model = message.Meta.Model ?? "modelo não informado";
            var usage = FmtTokens(u.Input) + " entrada · " + FmtTokens(u.Output) + " saída";
            var categories = (u.CacheReadInput is { } hit ? FmtTokens(hit) + " entrada em cache · " : "") +
                (u.CacheCreationInput is { } created ? FmtTokens(created) + " criação de cache · " : "") +
                (u.ReasoningOutput is { } thought ? FmtTokens(thought) + " raciocínio (parte da saída)" : "");
            var cost = u.Cost is { } value ? "US$ " + value.ToString("0.000000", CultureInfo.InvariantCulture) +
                (u.CostSource == "estimated" ? " estimado" : u.CostSource == "partial" ? " parcial" : " informado pelo provedor")
                : model == ModelId() && Estimate(u) is { } estimate && (u.CacheReadInput ?? 0) == 0 && (u.CacheCreationInput ?? 0) == 0
                    ? "US$ " + estimate.ToString("0.000000", CultureInfo.InvariantCulture) + " estimado pela tarifa cadastrada (não cobrado/confirmado)"
                    : "Custo desconhecido (tarifa de cache ou preço não informado)";
            body.Children.Add(Section(model, Wrap(usage, 12, UrbeTheme.Text2),
                Wrap(categories.Length > 0 ? categories.TrimEnd(' ', '·') : "Sem categorias adicionais informadas", 12, UrbeTheme.Text3),
                Wrap(cost, 12, UrbeTheme.Text2)));
        }
        body.Children.Add(Hint("Não há um teto financeiro obrigatório. Consulte a fatura do provedor para conciliação."));
        PerformanceSection(body);
    }

    /// <summary>
    /// CST-096: each agent's runs in this vault, as the owner asked them measured: time (median, p90) with completion,
    /// rework and checked edits. Only counts and durations, kept in the conversations of this device; can be turned off.
    /// </summary>
    private void PerformanceSection(StackPanel body)
    {
        var rows = new List<Control>();
        var byAgent = _store.ListConversations().Where(c => c.Vault == _app.VaultName).GroupBy(c => c.AgentId);
        foreach (var group in byAgent)
        {
            var stats = AiRunStats.Of(group.SelectMany(c => c.Runs));
            if (stats.Runs == 0) continue;
            var name = AgentNames.All(_cfg).FirstOrDefault(a => a.Id == group.Key) is { } agent ? AgentNames.CallName(agent, _cfg) : group.Key;
            rows.Add(Wrap(name + ": " + stats.Describe(), 12, UrbeTheme.Text2));
        }
        if (rows.Count == 0) rows.Add(Wrap("Sem execuções medidas ainda.", 12, UrbeTheme.Text3));
        var measure = new CheckBox { Content = "Medir execuções", IsChecked = _cfg.MeasureRuns, MinHeight = UrbeTokens.MinTouch };
        measure.IsCheckedChanged += (_, _) => { _cfg.MeasureRuns = measure.IsChecked == true; SaveConfig(); };
        rows.Add(measure);
        rows.Add(Hint("Guarda só tempos e contagens de cada execução (modelo, ferramentas, espera pela sua aprovação, chamadas, falhas, edições e conferências), nunca o conteúdo das notas ou dos pedidos. Fica neste aparelho, dentro de cada conversa, e some quando a conversa é apagada."));
        body.Children.Add(Section("Desempenho por agente", [.. rows]));
    }
}
