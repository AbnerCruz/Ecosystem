namespace Lunet;

public sealed partial class RandomSource
{
    /// <summary>Distribuição normal Box–Muller sem cache.</summary>
    /// <param name="mean">Média finita.</param>
    /// <param name="standardDeviation">Desvio padrão finito e não negativo.</param>
    /// <returns>Amostra da distribuição. Lança OverflowException se a saída exceder a faixa finita.</returns>
    public double NextGaussian(double mean = 0, double standardDeviation = 1)
    {
        if (!double.IsFinite(mean)) throw new ArgumentOutOfRangeException(nameof(mean));
        if (!double.IsFinite(standardDeviation) || standardDeviation < 0)
            throw new ArgumentOutOfRangeException(nameof(standardDeviation));
        if (standardDeviation == 0) return mean;
        var magnitude = Math.Sqrt(-2 * double.LogP1(-NextDouble()));
        var normal = magnitude * Math.Cos(Math.Tau * NextDouble());
        return FiniteSample(Math.FusedMultiplyAdd(standardDeviation, normal, mean));
    }

    /// <summary>Distribuição exponencial.</summary>
    /// <param name="rate">Taxa finita positiva.</param>
    /// <returns>Amostra não negativa. Lança OverflowException se a saída exceder a faixa finita.</returns>
    public double NextExponential(double rate)
    {
        if (!double.IsFinite(rate) || rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate));
        return FiniteSample(-double.LogP1(-NextDouble()) / rate);
    }

    private static double FiniteSample(double value) =>
        double.IsFinite(value) ? value : throw new OverflowException("O sorteio excede a faixa finita de double.");

    /// <summary>Distribuição triangular com limites incluídos.</summary>
    /// <param name="min">Limite inferior finito.</param>
    /// <param name="mode">Moda finita entre os limites.</param>
    /// <param name="max">Limite superior finito.</param>
    /// <returns>Amostra da distribuição.</returns>
    public double NextTriangular(double min, double mode, double max)
    {
        ValidateRange(min, max);
        if (!double.IsFinite(mode) || mode < min || mode > max) throw new ArgumentOutOfRangeException(nameof(mode));
        if (min == max) return min;

        var width = max - min;
        var pivot = double.IsFinite(width) ? (mode - min) / width : (mode / 2 - min / 2) / (max / 2 - min / 2);
        var unit = NextDouble();
        if (unit == 0) return min;
        var value = unit < pivot
            ? Interpolate(min, mode, Math.Sqrt(unit / pivot))
            : Interpolate(mode, max, 1 - Math.Sqrt((1 - unit) / (1 - pivot)));
        return Math.Clamp(value, min, max);
    }

    /// <summary>Sorteia por pesos relativos. Pesos zero são ignorados.</summary>
    /// <param name="weights">Pesos finitos não negativos, incluindo pelo menos um positivo.</param>
    /// <returns>Índice da posição sorteada.</returns>
    public int NextWeightedIndex(ReadOnlySpan<double> weights)
    {
        double maximum = 0;
        var last = -1;
        var positive = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            var weight = weights[i];
            if (!double.IsFinite(weight) || weight < 0) throw new ArgumentOutOfRangeException(nameof(weights));
            if (weight == 0) continue;
            maximum = Math.Max(maximum, weight);
            last = i;
            positive++;
        }
        if (positive == 0) throw new ArgumentException("Informe pelo menos um peso positivo.", nameof(weights));
        if (positive == 1) return last;

        double total = 0, error = 0;
        foreach (var weight in weights)
            if (weight > 0) Accumulate(weight / maximum, ref total, ref error);
        var target = Math.Min(NextDouble() * total, Math.BitDecrement(total));
        double current = 0;
        error = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] == 0) continue;
            Accumulate(weights[i] / maximum, ref current, ref error);
            if (current > target) return i;
        }
        return last;
    }

    private static void Accumulate(double value, ref double sum, ref double error)
    {
        var corrected = value - error;
        var next = sum + corrected;
        error = (next - sum) - corrected;
        sum = next;
    }

    /// <summary>Escolhe um elemento pelo índice.</summary>
    /// <param name="items">Coleção não vazia.</param>
    /// <returns>Elemento sorteado.</returns>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var count = items.Count;
        if (count == 0) throw new ArgumentException("A coleção está vazia.", nameof(items));
        return items[NextInt(count)];
    }

    /// <summary>Escolhe um elemento pelo peso relativo.</summary>
    /// <param name="items">Coleção não vazia.</param>
    /// <param name="weights">Um peso para cada elemento.</param>
    /// <returns>Elemento sorteado.</returns>
    public T PickWeighted<T>(IReadOnlyList<T> items, ReadOnlySpan<double> weights)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count != weights.Length) throw new ArgumentException("Informe um peso por elemento.", nameof(weights));
        return items[NextWeightedIndex(weights)];
    }

    /// <summary>Embaralha uma lista editável in-place por Fisher–Yates.</summary>
    /// <param name="items">Lista a embaralhar.</param>
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items is T[] array) { Shuffle(array); return; }
        if (items.Count < 2) return;
        if (items.IsReadOnly) throw new ArgumentException("A lista precisa permitir a troca de elementos.", nameof(items));
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>Embaralha uma região de memória por Fisher–Yates.</summary>
    /// <param name="items">Memória a embaralhar.</param>
    public void Shuffle<T>(Span<T> items)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>Embaralha o array in-place por Fisher–Yates.</summary>
    /// <param name="items">Array a embaralhar.</param>
    public void Shuffle<T>(T[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        // Covariant arrays support swapping their own values even if Span<T> rejects them.
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>Sorteia sem reposição por índice, preservando a coleção.</summary>
    /// <param name="items">Coleção a amostrar.</param>
    /// <param name="count">Número de índices, de zero ao tamanho da coleção.</param>
    /// <returns>Array da amostra.</returns>
    public T[] Sample<T>(IReadOnlyList<T> items, int count)
    {
        ArgumentNullException.ThrowIfNull(items);
        var size = items.Count;
        if (count < 0 || count > size) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return Array.Empty<T>();
        var result = new T[count];
        var remapped = new Dictionary<int, int>();
        for (int i = 0; i < count; i++)
        {
            var j = NextInt(i, size);
            result[i] = items[remapped.GetValueOrDefault(j, j)];
            remapped[j] = remapped.GetValueOrDefault(i, i);
            remapped.Remove(i);
        }
        return result;
    }
}
