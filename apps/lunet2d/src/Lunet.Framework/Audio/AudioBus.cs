namespace Lunet.Audio;

/// <summary>Grupo de volume (ex.: "Sfx", "Music"). O volume final de um som é instância × barramento × Master.</summary>
/// <example>
/// <code>
/// audio.Sfx.Volume = 0.5f;
/// audio.MusicBus.Muted = true;
/// </code>
/// </example>
public sealed class AudioBus
{
    private float _volume = 1f;

    internal AudioBus(string name) => Name = name;

    /// <summary>Nome do barramento.</summary>
    public string Name { get; }

    /// <summary>0 a 1.</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Silencia o barramento sem perder o volume ajustado.</summary>
    public bool Muted { get; set; }

    internal float Effective => Muted ? 0f : _volume;
}
