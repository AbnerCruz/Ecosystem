using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Lunet;

/// <summary>Posição e semente de um fluxo pseudoaleatório.</summary>
/// <param name="Version">Versão do formato.</param>
/// <param name="Seed">Semente de origem.</param>
/// <param name="S0">Primeira palavra do estado.</param>
/// <param name="S1">Segunda palavra do estado.</param>
/// <param name="S2">Terceira palavra do estado.</param>
/// <param name="S3">Quarta palavra do estado.</param>
/// <example><code>RandomState state = new RandomSource(42).CaptureState(); var copy = RandomSource.FromState(state);</code></example>
public readonly record struct RandomState(int Version, ulong Seed, ulong S0, ulong S1, ulong S2, ulong S3);

/// <summary>Gerador Xoshiro256** inicializado com SplitMix64, para jogos.</summary>
/// <remarks>Possui estado mutável. Use instâncias separadas ou sincronização externa entre threads.</remarks>
/// <example><code>var loot = new RandomSource(42).Fork("loot"); int roll = loot.NextInt(1, 7);</code></example>
public sealed partial class RandomSource
{
    private const int StateVersion = 1;
    private static ReadOnlySpan<byte> Prefix => "Lunet.Random.Fork.v1\0"u8;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private ulong _s0, _s1, _s2, _s3;

    /// <summary>Cria um fluxo com entropia do sistema operacional.</summary>
    public RandomSource() : this(CreateSeed()) { }

    /// <summary>Cria um fluxo reproduzível.</summary>
    /// <param name="seed">Semente de 64 bits, inclusive zero.</param>
    public RandomSource(ulong seed)
    {
        Seed = seed;
        _s0 = SplitMix64(ref seed);
        _s1 = SplitMix64(ref seed);
        _s2 = SplitMix64(ref seed);
        _s3 = SplitMix64(ref seed);
    }

    private RandomSource(RandomState state) => RestoreState(state);

    /// <summary>Semente de origem, usada na derivação de fluxos filhos.</summary>
    public ulong Seed { get; private set; }

    internal static ulong CreateSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    // Public-domain references by Blackman and Vigna, https://prng.di.unimi.it/
    private static ulong SplitMix64(ref ulong x)
    {
        unchecked
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>Avança a sequência de Xoshiro256**.</summary>
    /// <returns>Uma palavra de 64 bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextUInt64()
    {
        unchecked
        {
            var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
            var shifted = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= shifted;
            _s3 = BitOperations.RotateLeft(_s3, 45);
            return result;
        }
    }

    /// <summary>Extrai os 32 bits superiores da próxima palavra.</summary>
    /// <returns>Uma palavra de 32 bits.</returns>
    public uint NextUInt32() => (uint)(NextUInt64() >> 32);

    // Lemire's multiply-high with rejection of the excess preimages.
    private ulong Below(ulong width)
    {
        ulong high = Math.BigMul(NextUInt64(), width, out ulong low);
        if (low < width)
        {
            var threshold = unchecked(0UL - width) % width;
            while (low < threshold)
                high = Math.BigMul(NextUInt64(), width, out low);
        }
        return high;
    }

    /// <summary>Sorteia um inteiro não negativo.</summary>
    /// <returns>Um inteiro menor que int.MaxValue.</returns>
    public int NextInt() => NextInt(int.MaxValue);
    /// <summary>Sorteia um inteiro a partir de zero.</summary>
    /// <param name="maxExclusive">Limite superior excluído. Zero devolve zero.</param>
    /// <returns>Inteiro no intervalo pedido.</returns>
    public int NextInt(int maxExclusive) => NextInt(0, maxExclusive);
    /// <summary>Sorteia um inteiro com limite superior exclusivo.</summary>
    /// <param name="min">Limite inferior incluído.</param>
    /// <param name="maxExclusive">Limite superior excluído.</param>
    /// <returns>Inteiro sorteado, ou min quando o intervalo tem até uma saída.</returns>
    public int NextInt(int min, int maxExclusive)
    {
        if (maxExclusive < min) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        var width = (long)maxExclusive - min;
        return width <= 1 ? min : (int)(min + (long)Below((ulong)width));
    }

    /// <summary>Sorteia um inteiro longo não negativo.</summary>
    /// <returns>Um inteiro menor que long.MaxValue.</returns>
    public long NextInt64() => NextInt64(long.MaxValue);
    /// <summary>Sorteia um inteiro longo a partir de zero.</summary>
    /// <param name="maxExclusive">Limite superior excluído. Zero devolve zero.</param>
    /// <returns>Inteiro longo no intervalo pedido.</returns>
    public long NextInt64(long maxExclusive) => NextInt64(0, maxExclusive);
    /// <summary>Sorteia um inteiro longo com limite superior exclusivo.</summary>
    /// <param name="min">Limite inferior incluído.</param>
    /// <param name="maxExclusive">Limite superior excluído.</param>
    /// <returns>Inteiro sorteado, ou min quando o intervalo tem até uma saída.</returns>
    public long NextInt64(long min, long maxExclusive)
    {
        if (maxExclusive < min) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        unchecked
        {
            // An ordered signed difference lies in [0, 2^64−1], exactly the modular difference.
            var width = (ulong)maxExclusive - (ulong)min;
            return width <= 1 ? min : (long)((ulong)min + Below(width));
        }
    }

    internal static float UnitFloat(ulong word) => (word >> 40) * (1f / 16777216f);
    internal static double UnitDouble(ulong word) => (word >> 11) * (1d / 9007199254740992d);

    /// <summary>Extrai 24 bits de uma palavra nova.</summary>
    /// <returns>Real de precisão simples em [0, 1).</returns>
    public float NextFloat() => UnitFloat(NextUInt64());

    /// <summary>Extrai 53 bits de uma palavra nova.</summary>
    /// <returns>Real de precisão dupla em [0, 1).</returns>
    public double NextDouble() => UnitDouble(NextUInt64());

    /// <summary>Sorteia um real de precisão simples entre limites finitos.</summary>
    /// <param name="min">Limite inferior incluído.</param>
    /// <param name="maxExclusive">Limite superior excluído. Se igual a min, devolve min.</param>
    /// <returns>Real no intervalo pedido.</returns>
    public float NextFloat(float min, float maxExclusive)
    {
        ValidateRange(min, maxExclusive);
        if (min == maxExclusive) return min;
        var result = (float)(min + NextFloat() * ((double)maxExclusive - min));
        return result >= maxExclusive ? MathF.BitDecrement(maxExclusive) : MathF.Max(min, result);
    }

    /// <summary>Sorteia um real de precisão dupla entre limites finitos.</summary>
    /// <param name="min">Limite inferior incluído.</param>
    /// <param name="maxExclusive">Limite superior excluído. Se igual a min, devolve min.</param>
    /// <returns>Real no intervalo pedido.</returns>
    public double NextDouble(double min, double maxExclusive)
    {
        ValidateRange(min, maxExclusive);
        if (min == maxExclusive) return min;
        var result = Interpolate(min, maxExclusive, NextDouble());
        return result >= maxExclusive ? Math.BitDecrement(maxExclusive) : Math.Max(min, result);
    }

    private static void ValidateRange(double min, double maxExclusive)
    {
        if (!double.IsFinite(min)) throw new ArgumentOutOfRangeException(nameof(min));
        if (!double.IsFinite(maxExclusive) || maxExclusive < min)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
    }

    private static double Interpolate(double min, double max, double fraction)
    {
        var width = max - min;
        return double.IsFinite(width) ? min + fraction * width : (1 - fraction) * min + fraction * max;
    }

    /// <summary>Cara ou coroa com o bit superior da próxima palavra.</summary>
    /// <returns>Verdadeiro quando o bit vale um.</returns>
    public bool NextBool() => (NextUInt64() >> 63) != 0;

    /// <summary>Sorteia uma ocorrência na resolução de NextDouble.</summary>
    /// <param name="probability">Probabilidade finita de zero a um, inclusive.</param>
    /// <returns>Verdadeiro quando a ocorrência é sorteada.</returns>
    public bool Chance(double probability)
    {
        if (!double.IsFinite(probability) || probability < 0 || probability > 1)
            throw new ArgumentOutOfRangeException(nameof(probability));
        return probability == 1 || (probability != 0 && NextDouble() < probability);
    }

    /// <summary>Preenche bytes little-endian, usando uma palavra por bloco de oito ou cauda parcial.</summary>
    /// <param name="destination">Memória que receberá os bytes.</param>
    public void NextBytes(Span<byte> destination)
    {
        while (destination.Length >= 8)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination, NextUInt64());
            destination = destination[8..];
        }
        if (destination.IsEmpty) return;
        ulong tail = NextUInt64();
        for (int i = 0; i < destination.Length; i++)
            destination[i] = unchecked((byte)(tail >> (8 * i)));
    }

    /// <summary>Captura a posição exata sem alterar o fluxo.</summary>
    /// <returns>Versão, semente e as quatro palavras do estado.</returns>
    public RandomState CaptureState() => new(StateVersion, Seed, _s0, _s1, _s2, _s3);

    /// <summary>Restaura a semente e a posição a partir de um estado válido.</summary>
    /// <param name="state">Captura com a versão atual e ao menos uma palavra não nula.</param>
    public void RestoreState(RandomState state)
    {
        if (state.Version != StateVersion || (state.S0 | state.S1 | state.S2 | state.S3) == 0)
            throw new ArgumentException("Estado aleatório incompatível ou vazio.", nameof(state));
        Seed = state.Seed;
        (_s0, _s1, _s2, _s3) = (state.S0, state.S1, state.S2, state.S3);
    }

    /// <summary>Cria uma instância posicionada na captura.</summary>
    /// <param name="state">Versão, semente e posição do fluxo.</param>
    /// <returns>Gerador restaurado.</returns>
    public static RandomSource FromState(RandomState state) => new(state);

    /// <summary>Copia a sequência corrente.</summary>
    /// <returns>Gerador independente na mesma posição.</returns>
    public RandomSource Clone() => FromState(CaptureState());

    /// <summary>Deriva um filho estável sem avançar o pai.</summary>
    /// <param name="stream">Chave textual não vazia, UTF-8 estrito e comparação ordinal.</param>
    /// <returns>Gerador com a semente derivada.</returns>
    public RandomSource Fork(string stream)
    {
        ArgumentException.ThrowIfNullOrEmpty(stream);
        var length = StrictUtf8.GetByteCount(stream);
        var offset = Prefix.Length + 8;
        var size = checked(offset + 5 + length);
        Span<byte> input = size <= 256 ? stackalloc byte[size] : new byte[size];
        WriteForkHeader(input, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(input[(offset + 1)..], (uint)length);
        StrictUtf8.GetBytes(stream, input[(offset + 5)..]);
        return HashFork(input);
    }

    /// <summary>Deriva um filho estável sem avançar o pai.</summary>
    /// <param name="stream">Chave numérica do filho.</param>
    /// <returns>Gerador com a semente derivada.</returns>
    public RandomSource Fork(ulong stream)
    {
        Span<byte> input = stackalloc byte[Prefix.Length + 17];
        WriteForkHeader(input, 2);
        BinaryPrimitives.WriteUInt64LittleEndian(input[^8..], stream);
        return HashFork(input);
    }

    private void WriteForkHeader(Span<byte> input, byte tag)
    {
        Prefix.CopyTo(input);
        BinaryPrimitives.WriteUInt64LittleEndian(input[Prefix.Length..], Seed);
        input[Prefix.Length + 8] = tag;
    }

    private static RandomSource HashFork(ReadOnlySpan<byte> input)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        return new RandomSource(BinaryPrimitives.ReadUInt64LittleEndian(digest));
    }

    /// <summary>Avança 2^128 passos, preservando a semente.</summary>
    public void Jump() => ApplyJump([0x180EC6D33CFD0ABAUL, 0xD5A61266F0C9392CUL, 0xA9582618E03FC9AAUL, 0x39ABDC4529B1661CUL]);

    /// <summary>Avança 2^192 passos, preservando a semente.</summary>
    public void LongJump() => ApplyJump([0x76E15D3EFEFDCBBFUL, 0xC5004E441C522FB3UL, 0x77710069854EE241UL, 0x39109BB02ACBE635UL]);

    private void ApplyJump(ReadOnlySpan<ulong> polynomial)
    {
        ulong s0 = 0, s1 = 0, s2 = 0, s3 = 0;
        foreach (var coefficient in polynomial)
        for (int bit = 0; bit < 64; bit++)
        {
            if ((coefficient & (1UL << bit)) != 0)
            {
                s0 ^= _s0;
                s1 ^= _s1;
                s2 ^= _s2;
                s3 ^= _s3;
            }
            NextUInt64();
        }
        (_s0, _s1, _s2, _s3) = (s0, s1, s2, s3);
    }
}
