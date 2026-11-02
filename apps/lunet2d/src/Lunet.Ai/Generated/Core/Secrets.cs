namespace AgentRuntime;

/// <summary>
/// Referência a um segredo. O agente recebe a REFERÊNCIA ou a permissão de uso, nunca o valor (plano §8.2): quem resolve é o
/// adapter do provedor, por dentro. O valor nunca entra em prompt, log, memória de agente, arquivo, commit ou artefato.
/// </summary>
public sealed record SecretRef(string Name);

/// <summary>Porta de armazenamento de segredos (a implementação por plataforma é decisão futura; o Core só conhece a porta).</summary>
public interface ISecretStore
{
    ValueTask<string?> ResolveAsync(SecretRef reference, CancellationToken cancellationToken);
}

/// <summary>
/// Defesa em profundidade: remove dos textos que vão ao log, ao modelo ou aos artefatos qualquer valor de segredo conhecido.
/// Quem entrega segredos (o store do Host) os registra aqui. Segredo curto demais não pode ser redigido sem corromper o texto, então é recusado.
/// </summary>
public sealed class SecretRedactor
{
    public const int MinimumLength = 8;
    public const string Mask = "[REDACTED]";

    private readonly object _gate = new();
    private readonly List<string> _values = [];

    public static SecretRedactor None { get; } = new();

    public void Register(string secretValue)
    {
        if (string.IsNullOrEmpty(secretValue) || secretValue.Length < MinimumLength)
            throw new ArgumentException($"Segredos precisam de ao menos {MinimumLength} caracteres para serem redigidos sem corromper o texto.", nameof(secretValue));
        lock (_gate)
        {
            if (_values.Contains(secretValue, StringComparer.Ordinal)) return;
            _values.Add(secretValue);
            _values.Sort((a, b) => b.Length.CompareTo(a.Length)); // o mais longo primeiro: um segredo que contém outro some inteiro
        }
    }

    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string[] snapshot;
        lock (_gate) snapshot = [.. _values];
        foreach (var value in snapshot) text = text.Replace(value, Mask, StringComparison.Ordinal);
        return text;
    }
}
