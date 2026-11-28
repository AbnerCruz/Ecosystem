using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lunet.Ai;

/// <summary>
/// What the person wants from a message (LUNET-498). <see cref="Teach"/>: learn how to do it themselves (explain, the
/// project is never changed). <see cref="Do"/>: the AI builds it in the project. <see cref="Auto"/>: decided per message.
/// </summary>
public enum LunetAiMode { Auto, Teach, Do }

/// <summary>Decides between teaching and doing. Asking "how do I…" must never make the AI change the project on its own.</summary>
public static class LunetAiIntent
{
    private const string DoVerbs =
        "faz|faze|fazer|faca|crie|cria|criar|implementa|implementar|implemente|adiciona|adicionar|adicione|coloca|colocar|coloque|" +
        "corrige|corrigir|corrija|conserta|consertar|conserte|arruma|arrumar|arrume|muda|mudar|mude|troca|trocar|troque|gera|gerar|gere|" +
        "desenha|desenhar|desenhe|escreve|escrever|escreva|refatora|refatorar|refatore|remove|remover|remova|apaga|apagar|apague|" +
        "organiza|organizar|organize|separa|separar|separe|melhora|melhorar|melhore|otimiza|otimizar|otimize|aplica|aplicar|aplique|" +
        "continua|continuar|continue|termina|terminar|termine|monta|montar|monte|configura|configurar|configure|ajusta|ajustar|ajuste|" +
        "bota|botar|bote|renomeia|renomear|renomeie|deixa|deixar|deixe";

    // Explicit delegation ("faz pra mim", "pode criar…?", "quero que você implemente") wins over any question form.
    private static readonly Regex Delegation = new(
        @"\b(" + DoVerbs + @")\b.{0,40}\b(pra|para|por) mim\b" +
        @"|\b(pode|poderia|consegue|conseguiria|da pra|dá pra)( (voce|vc))?( me)? (" + DoVerbs + @")\b" +
        @"|\b(quero|preciso) que (voce|vc|a ia) (" + DoVerbs + @"|faca|implemente|crie)\b" +
        @"|\bfaz (ai|isso|logo|tudo)\b|\bmao na massa\b|\bfazer por mim\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Learning = new(
        @"\b(como (eu )?(faco|fazer|faz|crio|criar|cria|uso|usar|usa|posso|devo|consigo|implemento|implementar|adiciono|adicionar|coloco|colocar|funciona|funcionam|seria|e que|programo|programar|deixo|faria|mudo|mudar|troco|desenho|animo|toco|salvo)" +
        @"|me (explica|explique|ensina|ensine|mostra como|mostre como|ajuda a entender|ajude a entender)|explica(r)?\b|explique|ensina(r)?\b|ensine" +
        @"|o que (e|sao|significa|faz|fazem)\b|qual (e )?a diferenca|por que|porque (isso|o|a)|pra que serve|para que serve" +
        @"|quero aprender|quero entender|aprender a|entender como|passo a passo|tutorial|duvida|onde (fica|eu|mexo|coloco)|qual (classe|metodo|funcao|api))",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Imperative = new(
        @"^(por favor |pf |pfv )?(faz|faca|crie|cria|implementa|implemente|adiciona|adicione|coloca|coloque|corrige|corrija|conserta|conserte|arruma|arrume|muda|mude|troca|troque|gera|gere|desenha|desenhe|escreve|escreva|refatora|refatore|remove|remova|apaga|apague|organiza|organize|separa|separe|melhora|melhore|otimiza|otimize|aplica|aplique|continua|continue|termina|termine|monta|monte|configura|configure|ajusta|ajuste|bota|bote|renomeia|renomeie|deixa|deixe|quero|preciso|faca)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The mode actually used for <paramref name="prompt"/>: an explicit choice wins; Auto classifies.</summary>
    public static LunetAiMode Resolve(LunetAiMode chosen, string prompt) =>
        chosen == LunetAiMode.Auto ? Classify(prompt) : chosen;

    public static LunetAiMode Classify(string prompt)
    {
        var text = Normalize(prompt);
        if (text.Length == 0) return LunetAiMode.Teach;
        if (Delegation.IsMatch(text)) return LunetAiMode.Do;
        if (Learning.IsMatch(text)) return LunetAiMode.Teach;
        if (Imperative.IsMatch(text)) return LunetAiMode.Do;
        // A bare question without an order is a question; anything else is a request to build.
        return text.TrimEnd().EndsWith('?') ? LunetAiMode.Teach : LunetAiMode.Do;
    }

    /// <summary>Lowercase, without accents and with single spaces, so "Como faço?" and "como faco" match.</summary>
    public static string Normalize(string text)
    {
        var decomposed = (text ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        bool space = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c)) { if (!space && builder.Length > 0) builder.Append(' '); space = true; continue; }
            builder.Append(c);
            space = false;
        }
        return builder.ToString().TrimEnd();
    }

    /// <summary>Shared collaboration policy: preserve the person's reasoning, not merely their file layout.</summary>
    public const string Collaboration = 
        "AUTORIA DO USUÁRIO. Antes de sugerir ou editar código existente, entenda a intenção, leia a implementação e identifique o que já funciona. " +
        "Distinga erro demonstrável, risco e preferência de estilo; explique a causa com um caso concreto, sem concordância automática. " +
        "Preserve nomes, APIs, estrutura e lógica válidos; prefira a menor alteração suficiente, sem refatoração ou abstração não solicitada. " +
        "Se a lógica não puder atender ao objetivo com uma correção localizada, explique por quê e apresente a alternativa e suas consequências; " +
        "não imponha uma reescrita por gosto. Uma reformulação explicitamente solicitada é permitida.";

    /// <summary>Instructions added to the task for the resolved mode.</summary>
    public static string Instructions(LunetAiMode mode) => Collaboration + "\n\n" + (mode == LunetAiMode.Teach
        ? "MODO ENSINAR (a pessoa quer aprender a fazer sozinha). Você NÃO pode alterar o projeto nesta resposta. " +
          "Adapte a ajuda ao pedido e ao conhecimento demonstrado: primeiro explique o comportamento atual e o erro, se houver, " +
          "usando arquivos, membros e linhas reais quando o contexto estiver disponível. Se faltar código, leia-o ou declare a limitação. " +
          "Ofereça uma pista ou uma pequena experiência de diagnóstico e explique o conceito; não entregue uma solução completa por padrão. " +
          "Quando a pessoa pedir código ou uma solução explícita, mostre a correção localizada ou o exemplo necessário, sem exigir etapas artificiais. " +
          "Uma alternativa mais ampla deve ser identificada como alternativa e justificada, não apresentada como substituição obrigatória. " +
          "Sugira como verificar o resultado quando pertinente. Não insista em delegar a execução nem termine sempre oferecendo Fazer por mim."
        : "MODO FAZER (a pessoa pediu para você construir). Implemente no projeto, respeitando a lógica existente. " +
          "Use alterações localizadas; peça autorização antes de uma mudança arquitetural relevante que ultrapasse o pedido. " +
          "Compile e corrija os erros pertinentes até compilar; informe bloqueios reais e erros preexistentes sem apagar código alheio. " +
          "No final, resuma em poucas linhas o que mudou (arquivos e classes) e como testar.");
}
