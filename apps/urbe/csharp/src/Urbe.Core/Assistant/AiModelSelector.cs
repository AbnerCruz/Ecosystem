namespace Urbe.Core.Assistant;

/// <summary>What a task needs from a model: what goes in, what must come out, tools, and how many tokens it should take.</summary>
public sealed record AiTaskNeeds(AiModalities Input = AiModalities.Text, AiModalities Output = AiModalities.Text, bool Tools = false,
    long InputTokens = 0, long OutputTokens = 0);

/// <summary>How the choice came out.</summary>
public enum AiSelectionOutcome
{
    /// <summary>The person's model does it.</summary>
    Chosen,
    /// <summary>The person's model is used, but its provider does not say whether it does this.</summary>
    Unverified,
    /// <summary>No model was chosen; the catalog picked one that does it.</summary>
    Automatic,
    /// <summary>The person's model does not do it; <see cref="AiSelection.Fallback"/> does (offered, never applied silently).</summary>
    Fallback,
    /// <summary>Only a model that would send the data out of the device does it, and that was not allowed.</summary>
    NeedsConsent,
    /// <summary>No configured model does it.</summary>
    Missing
}

/// <summary>An estimate before spending: US$ when a price is known (0 on the device), with what it is based on.</summary>
public sealed record AiCostEstimate(double? Usd, string Text);

/// <summary>The selector's answer: the model to use, why, the fallback when the chosen one cannot, what is missing, the estimate.</summary>
public sealed record AiSelection(AiSelectionOutcome Outcome, AiModelCapabilities? Model, AiModelCapabilities? Fallback,
    AiModalities Missing, AiCostEstimate Cost, string Reason)
{
    /// <summary>The model that would do the work if the person agrees (the chosen one, or the offered fallback).</summary>
    public AiModelCapabilities? Capable => Outcome is AiSelectionOutcome.Fallback or AiSelectionOutcome.NeedsConsent ? Fallback
        : Outcome == AiSelectionOutcome.Missing ? null : Model;
}

/// <summary>Whether and how the app can make a video now: programmatic (rendered here) or generative (an external model).</summary>
public sealed record AiVideoRoute(bool Available, bool Programmatic, AiModelCapabilities? Model, string Text);

/// <summary>
/// CST-220: picks a model for a task from <see cref="AiModelCatalog"/>. The person's explicit choice wins whenever it can do
/// the task; when it cannot, the answer says so and names a capable fallback (preferring the same provider, then the device,
/// then the cheapest) instead of switching. A model that sends data out of the device is only offered when that is allowed.
/// Unknown capabilities are never treated as present or absent: the person's model is then used and the doubt reported.
/// </summary>
public static class AiModelSelector
{
    private enum Fit { Yes, Unknown, No }

    private static (Fit Fit, AiModalities Missing, string? Why) Check(AiModelCapabilities m, AiTaskNeeds n)
    {
        var missing = AiModalities.None;
        bool unknown = false;
        string? why = null;
        foreach (AiModalities f in Enum.GetValues<AiModalities>())
        {
            if (f == AiModalities.None) continue;
            if (n.Input.HasFlag(f))
            {
                // the app only sends text, images and PDFs: other inputs are missing whatever the model takes
                if (!AiModelCatalog.AppSends.HasFlag(f)) missing |= f;
                else if (m.Reads(f) is { } r) { if (!r) missing |= f; }
                else unknown = true;
            }
            if (n.Output.HasFlag(f))
            {
                if (m.Makes(f) is { } r) { if (!r) missing |= f; }
                else unknown = true;
            }
        }
        if (n.Tools) { if (m.Tools == false) why = "não usa ferramentas"; else if (m.Tools is null) unknown = true; }
        if (n.InputTokens > 0 && m.Context is { } ctx && n.InputTokens + n.OutputTokens > ctx)
            why = (why is null ? "" : why + "; ") + "o pedido não cabe no contexto (" + ctx + " tokens)";
        if (missing != AiModalities.None || why is not null) return (Fit.No, missing, why);
        return (unknown ? Fit.Unknown : Fit.Yes, AiModalities.None, null);
    }

    /// <summary>The estimate of the task on <paramref name="m"/>; never "free" when the price is not known.</summary>
    public static AiCostEstimate Estimate(AiModelCapabilities? m, AiTaskNeeds n)
    {
        if (m is null) return new(null, "Sem modelo, sem gasto.");
        if (m.Place == AiModelPlace.Device) return new(0, "Sem cobrança: roda no aparelho.");
        if (n.Output.HasFlag(AiModalities.Image) && m.Makes(AiModalities.Image) == true)
            return new(null, m.ImageRate is { } rate ? "Tarifa: " + rate + " (estimativa; a cobrança final é do provedor)." : "Preço da imagem não informado · não significa gratuito.");
        if (m.Price is { } p && n.InputTokens + n.OutputTokens > 0)
        {
            var usd = (n.InputTokens * p.Input + n.OutputTokens * p.Output) / 1e6;
            return new(usd, AiModelCatalog.Usd(usd) + " estimado (" + p.Source + "; a cobrança real é a do provedor).");
        }
        if (m.Price is not null) return new(null, "Tarifa: " + AiModelCatalog.PriceText(m) + ".");
        return new(null, m.Place == AiModelPlace.OwnServer ? "Servidor seu: sem tarifa informada." : "Preço não informado · não significa gratuito.");
    }

    private static string What(AiTaskNeeds n, AiModalities missing)
    {
        var parts = new List<string>();
        if ((missing & ~AiModelCatalog.AppSends & n.Input) is var notSent and not AiModalities.None) parts.Add("receber " + AiModelCatalog.ModalityNames(notSent));
        if ((missing & AiModelCatalog.AppSends & n.Input) is var read and not AiModalities.None) parts.Add("ler " + AiModelCatalog.ModalityNames(read));
        if ((missing & n.Output) is var made and not AiModalities.None) parts.Add("gerar " + AiModelCatalog.ModalityNames(made));
        return parts.Count == 0 ? "fazer isso" : string.Join(" e ", parts);
    }

    /// <summary>Chooses for <paramref name="needs"/> among <paramref name="candidates"/>.</summary>
    public static AiSelection Select(AiTaskNeeds needs, IReadOnlyList<AiModelCapabilities> candidates, AiModelChoice? preferred = null, bool allowRemote = true)
    {
        var chosen = preferred is null ? null : candidates.FirstOrDefault(c => c.ProviderId == preferred.ProviderId && c.ModelId == preferred.Model);
        var capable = candidates.Where(c => Check(c, needs).Fit == Fit.Yes).ToList();
        AiModelCapabilities? Best(IEnumerable<AiModelCapabilities> pool) => pool
            .OrderBy(c => chosen is not null && c.ProviderId == chosen.ProviderId ? 0 : 1)
            .ThenBy(c => c.Place switch { AiModelPlace.Device => 0, AiModelPlace.OwnServer => 1, _ => 2 })
            .ThenBy(c => Estimate(c, needs).Usd ?? (c.Price is { } p ? (p.Input + p.Output) / 1e6 : double.MaxValue))
            .ThenByDescending(c => c.Context ?? 0)
            .FirstOrDefault();
        var allowed = Best(capable.Where(c => allowRemote || c.Place != AiModelPlace.Remote));
        var remoteOnly = allowed is null ? Best(capable) : null;

        if (chosen is not null)
        {
            var (fit, missing, why) = Check(chosen, needs);
            if (fit == Fit.Yes) return new(AiSelectionOutcome.Chosen, chosen, null, AiModalities.None, Estimate(chosen, needs), chosen.Label + " faz isso.");
            if (fit == Fit.Unknown)
                return new(AiSelectionOutcome.Unverified, chosen, null, AiModalities.None, Estimate(chosen, needs),
                    chosen.Label + " é o modelo escolhido; " + chosen.ProviderName + " não informa se ele consegue " + What(needs, needs.Input | needs.Output) + ".");
            var cannot = chosen.Label + " não consegue " + What(needs, missing) + (why is not null ? " (" + why + ")" : "") + ".";
            if (allowed is not null)
                return new(AiSelectionOutcome.Fallback, chosen, allowed, missing, Estimate(allowed, needs),
                    cannot + " " + allowed.Label + " consegue" + Privacy(chosen, allowed) + ".");
            if (remoteOnly is not null)
                return new(AiSelectionOutcome.NeedsConsent, chosen, remoteOnly, missing, Estimate(remoteOnly, needs),
                    cannot + " Só " + remoteOnly.Label + " consegue, e isso enviaria os dados para fora do aparelho.");
            return new(AiSelectionOutcome.Missing, chosen, null, missing, Estimate(null, needs), cannot + " Nenhum modelo configurado faz isso.");
        }
        if (allowed is not null) return new(AiSelectionOutcome.Automatic, allowed, null, AiModalities.None, Estimate(allowed, needs), allowed.Label + " faz isso.");
        if (remoteOnly is not null)
            return new(AiSelectionOutcome.NeedsConsent, null, remoteOnly, AiModalities.None, Estimate(remoteOnly, needs),
                "Só " + remoteOnly.Label + " faz isso, e isso enviaria os dados para fora do aparelho.");
        var missingAll = (needs.Input | needs.Output) & ~AiModalities.Text;
        return new(AiSelectionOutcome.Missing, null, null, missingAll, Estimate(null, needs),
            "Nenhum modelo configurado consegue " + What(needs, missingAll == AiModalities.None ? needs.Input : missingAll) + ".");
    }

    private static string Privacy(AiModelCapabilities from, AiModelCapabilities to) =>
        !from.LeavesDevice && to.LeavesDevice ? ", mas o conteúdo sairá do aparelho para " + to.ProviderName
        : from.ProviderId != to.ProviderId && to.Place == AiModelPlace.Remote ? " (o conteúdo vai para " + to.ProviderName + ")" : "";

    /// <summary>
    /// How a video would be made now. Programmatic video (rendered on the device from the vault, no model, no cost) needs the
    /// renderer; generative video needs an external model that outputs video. Neither is pretended.
    /// </summary>
    public static AiVideoRoute Video(IReadOnlyList<AiModelCapabilities> candidates, bool programmaticRenderer = false, bool allowRemote = true)
    {
        if (programmaticRenderer)
            return new(true, true, null, "Vídeo programático: renderizado no aparelho, a partir do vault; nada é enviado e não há custo de modelo.");
        var generative = candidates.Where(c => c.Makes(AiModalities.Video) == true).OrderBy(c => c.Place).FirstOrDefault();
        if (generative is not null && (allowRemote || generative.Place != AiModelPlace.Remote))
            return new(true, false, generative, "Vídeo generativo externo: " + generative.Label +
                (generative.LeavesDevice ? "; a descrição sai do aparelho e precisa de " + generative.HostRequirement : "") + ". " + Estimate(generative, new(Output: AiModalities.Video)).Text);
        return new(false, false, generative,
            "Vídeo indisponível: este aparelho ainda não tem o renderizador de vídeo programático do Urbe, e " +
            (generative is null ? "nenhum modelo configurado gera vídeo (um vídeo generativo exigiria um provedor externo, com envio e cobrança)."
                : "o único modelo que gera vídeo, " + generative.Label + ", enviaria os dados para fora do aparelho."));
    }

    /// <summary>The needs of a chat message from its attachments' types.</summary>
    public static AiTaskNeeds ForAttachments(IEnumerable<string> mimeTypes, bool tools = false)
    {
        var input = AiModalities.Text;
        foreach (var mime in mimeTypes)
            input |= mime == "application/pdf" ? AiModalities.Pdf
                : mime.StartsWith("image/", StringComparison.Ordinal) ? AiModalities.Image
                : mime.StartsWith("audio/", StringComparison.Ordinal) ? AiModalities.Audio
                : mime.StartsWith("video/", StringComparison.Ordinal) ? AiModalities.Video : AiModalities.None;
        return new(input, AiModalities.Text, tools);
    }
}
