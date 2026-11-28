namespace Lunet;

/// <summary>Campo Philox4x32-10 imutável, indexado por coordenada, canal e amostra.</summary>
/// <example><code>var field = new RandomField(42); float value = field.FloatAt(-3, 12, channel: 1);</code></example>
public sealed class RandomField
{
    /// <summary>Cria o campo com entropia do sistema operacional.</summary>
    public RandomField() : this(RandomSource.CreateSeed()) { }

    /// <summary>Cria um campo reproduzível.</summary>
    /// <param name="seed">Chave de 64 bits.</param>
    public RandomField(ulong seed) => Seed = seed;

    /// <summary>Chave de origem do campo.</summary>
    public ulong Seed { get; }

    /// <summary>Calcula a amostra indexada. A saída truncada pode ter colisões.</summary>
    /// <param name="x">Coordenada horizontal, inclusive negativa.</param>
    /// <param name="y">Coordenada vertical, inclusive negativa.</param>
    /// <param name="channel">Canal daquela posição.</param>
    /// <param name="sample">Índice da amostra no canal.</param>
    /// <returns>Palavra de 64 bits independente da ordem das consultas.</returns>
    public ulong UInt64At(int x, int y, uint channel = 0, uint sample = 0)
    {
        // Philox4x32-10, Salmon et al., SC11 / Random123.
        unchecked
        {
            uint c0 = (uint)x, c1 = (uint)y, c2 = channel, c3 = sample;
            uint k0 = (uint)Seed, k1 = (uint)(Seed >> 32);
            for (int round = 0; round < 10; round++)
            {
                ulong p0 = 0xD2511F53UL * c0, p1 = 0xCD9E8D57UL * c2;
                (c0, c1, c2, c3) = ((uint)(p1 >> 32) ^ c1 ^ k0, (uint)p1, (uint)(p0 >> 32) ^ c3 ^ k1, (uint)p0);
                k0 += 0x9E3779B9;
                k1 += 0xBB67AE85;
            }
            return c0 | ((ulong)c1 << 32);
        }
    }

    /// <summary>Calcula uma amostra real de precisão simples.</summary>
    /// <param name="x">Coordenada horizontal.</param>
    /// <param name="y">Coordenada vertical.</param>
    /// <param name="channel">Canal da posição.</param>
    /// <param name="sample">Índice no canal.</param>
    /// <returns>Real em [0, 1).</returns>
    public float FloatAt(int x, int y, uint channel = 0, uint sample = 0) => RandomSource.UnitFloat(UInt64At(x, y, channel, sample));

    /// <summary>Calcula uma amostra real de precisão dupla.</summary>
    /// <param name="x">Coordenada horizontal.</param>
    /// <param name="y">Coordenada vertical.</param>
    /// <param name="channel">Canal da posição.</param>
    /// <param name="sample">Índice no canal.</param>
    /// <returns>Real em [0, 1).</returns>
    public double DoubleAt(int x, int y, uint channel = 0, uint sample = 0) => RandomSource.UnitDouble(UInt64At(x, y, channel, sample));

    /// <summary>Cria um fluxo sequencial local.</summary>
    /// <param name="x">Coordenada horizontal.</param>
    /// <param name="y">Coordenada vertical.</param>
    /// <param name="channel">Canal da posição.</param>
    /// <param name="sample">Índice no canal.</param>
    /// <returns>Gerador iniciado pela palavra da chave completa.</returns>
    public RandomSource StreamAt(int x, int y, uint channel = 0, uint sample = 0) => new(UInt64At(x, y, channel, sample));
}
