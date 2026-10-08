namespace Lunet.Graphics;

/// <summary>Quadro de animação: região da textura e tempo de exibição em segundos.</summary>
/// <example><code>var frame = new SpriteAnimationFrame(new RectangleF(0, 0, 16, 16), 0.1);</code></example>
public readonly struct SpriteAnimationFrame
{
    /// <summary>Define um quadro. A validade da região e duração é conferida pelo clip.</summary>
    /// <param name="source">Região em pixels.</param>
    /// <param name="durationSeconds">Tempo positivo e finito de exibição.</param>
    public SpriteAnimationFrame(RectangleF source, double durationSeconds)
    {
        Source = source;
        DurationSeconds = durationSeconds;
    }
    /// <summary>Região em pixels da textura.</summary>
    public RectangleF Source { get; }
    /// <summary>Duração do quadro em segundos.</summary>
    public double DurationSeconds { get; }
}

/// <summary>Sequência imutável de quadros; pode ser compartilhada por vários animadores independentes.</summary>
/// <example><code>
/// var sheet = new SpriteSheet(texture, 16, 16);
/// var clip = SpriteAnimationClip.FromSheet(sheet, new[] { 0, 1, 2, 1 }, 0.1);
/// </code></example>
/// <remarks>Copia os quadros na criação. Não possui nem libera a textura. As regiões podem ter tamanhos diferentes e vir de um atlas.</remarks>
public sealed class SpriteAnimationClip
{
    private readonly SpriteAnimationFrame[] _frames;
    private readonly double[] _ends;

    /// <summary>Cria um clip com duração individual por quadro.</summary>
    /// <param name="texture">Textura compartilhada; deve continuar viva durante o desenho.</param>
    /// <param name="frames">Quadros não vazios, com regiões dentro da textura e duração positiva/finita.</param>
    /// <param name="loop">Repetir ao chegar ao fim; falso mantém o último quadro.</param>
    public SpriteAnimationClip(Texture2D texture, IReadOnlyList<SpriteAnimationFrame> frames, bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0) throw new ArgumentException("O clip precisa de quadros.", nameof(frames));
        Texture = texture;
        Loop = loop;
        _frames = new SpriteAnimationFrame[frames.Count];
        _ends = new double[frames.Count];
        double total = 0;
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames[i];
            var r = frame.Source;
            if (!float.IsFinite(r.X) || !float.IsFinite(r.Y) || !float.IsFinite(r.Width) || !float.IsFinite(r.Height)
                || r.X < 0 || r.Y < 0 || r.Width <= 0 || r.Height <= 0
                || (double)r.X + r.Width > texture.Width || (double)r.Y + r.Height > texture.Height)
                throw new ArgumentException("Região inválida ou fora da textura.", nameof(frames));
            var end = total + frame.DurationSeconds;
            if (!double.IsFinite(frame.DurationSeconds) || frame.DurationSeconds <= 0 || !double.IsFinite(end) || end <= total)
                throw new ArgumentException("Duração inválida ou sem precisão na linha do tempo.", nameof(frames));
            _frames[i] = frame;
            _ends[i] = total = end;
        }
        DurationSeconds = total;
    }

    /// <summary>Textura compartilhada dos quadros.</summary>
    public Texture2D Texture { get; }
    /// <summary>Número de quadros.</summary>
    public int FrameCount => _frames.Length;
    /// <summary>Duração de um ciclo completo em segundos.</summary>
    public double DurationSeconds { get; }
    /// <summary>Se a sequência repete.</summary>
    public bool Loop { get; }
    /// <summary>Consulta um quadro por índice, sem expor o array interno.</summary>
    /// <param name="index">Índice de zero a FrameCount−1.</param>
    /// <returns>Região e duração do quadro.</returns>
    public SpriteAnimationFrame Frame(int index)
    {
        if ((uint)index >= (uint)_frames.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return _frames[index];
    }

    /// <summary>Cria uma sequência de quadros de tamanho igual, com duração uniforme.</summary>
    /// <param name="sheet">Spritesheet.</param>
    /// <param name="indices">Ordem dos quadros; repetição de índices é permitida.</param>
    /// <param name="frameSeconds">Duração positiva/finita de cada quadro.</param>
    /// <param name="loop">Repetir a sequência.</param>
    /// <returns>Clip imutável.</returns>
    public static SpriteAnimationClip FromSheet(SpriteSheet sheet, IReadOnlyList<int> indices, double frameSeconds, bool loop = true)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(indices);
        var frames = new SpriteAnimationFrame[indices.Count];
        for (var i = 0; i < indices.Count; i++) frames[i] = new(sheet.Frame(indices[i]), frameSeconds);
        return new SpriteAnimationClip(sheet.Texture, frames, loop);
    }

    internal int FindFrame(double seconds)
    {
        // Primeiro fim estritamente maior que o tempo. No fim de um one-shot mantém o último quadro.
        int low = 0, high = _ends.Length - 1;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (seconds < _ends[mid]) high = mid;
            else low = mid + 1;
        }
        return low;
    }
}
