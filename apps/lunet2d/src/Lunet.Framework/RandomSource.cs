using System.Numerics;

namespace Lunet;

/// <summary>Gerador pseudoaleatório determinístico (xorshift64*): mesma semente, mesma sequência, em qualquer aparelho.</summary>
/// <example>
/// <code>
/// var dice = new RandomSource(42);
/// int roll = dice.NextInt(1, 7);
/// Vector2 direction = dice.NextDirection();
/// </code>
/// </example>
public sealed class RandomSource
{
    private ulong _state;

    /// <summary>Cria um gerador determinístico.</summary>
    /// <param name="seed">Semente: a mesma semente gera a mesma sequência.</param>
    public RandomSource(ulong seed)
    {
        // Mistura a semente (splitmix64) para que sementes pequenas e vizinhas divirjam.
        var z = seed + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        _state = z ^ (z >> 31);
        if (_state == 0) _state = 1;
    }

    /// <summary>Próximo número de 64 bits sem sinal.</summary>
    /// <returns>Número pseudoaleatório.</returns>
    public ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Número em [0, 1).</summary>
    /// <returns>Número de 0 (incluído) a 1 (não incluído), ou dentro do intervalo pedido.</returns>
    public float NextFloat() => (NextUInt64() >> 40) / (float)(1 << 24);

    /// <summary>Número decimal no intervalo dado.</summary>
    /// <param name="min">Menor valor (inclusive).</param>
    /// <param name="max">Maior valor (exclusive).</param>
    /// <returns>Número entre min e max.</returns>
    public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

    /// <summary>Inteiro em [0, <paramref name="maxExclusive"/>).</summary>
    /// <param name="maxExclusive">Limite superior, não incluído.</param>
    /// <returns>Número inteiro dentro do intervalo pedido.</returns>
    public int NextInt(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxExclusive, 0);
        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    /// <summary>Inteiro em [<paramref name="min"/>, <paramref name="maxExclusive"/>).</summary>
    /// <param name="min">Menor valor possível (incluído).</param>
    /// <param name="maxExclusive">Limite superior, não incluído.</param>
    /// <returns>Número inteiro dentro do intervalo pedido.</returns>
    public int NextInt(int min, int maxExclusive) => min + NextInt(maxExclusive - min);

    /// <summary>Cara ou coroa.</summary>
    /// <returns>Verdadeiro ou falso com igual chance.</returns>
    public bool NextBool() => (NextUInt64() & 1) == 1;

    /// <summary>Direção aleatória de comprimento 1.</summary>
    /// <returns>Vetor unitário.</returns>
    public Vector2 NextDirection()
    {
        var angle = NextFloat() * MathF.Tau;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}
