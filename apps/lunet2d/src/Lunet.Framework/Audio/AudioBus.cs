namespace Lunet.Audio;

/// <summary>Grupo de volume (ex.: "Sfx", "Music"). O volume final de um som é instância × barramento × Master.</summary>
public sealed class AudioBus
{
    private float _volume = 1f;

    internal AudioBus(string name) => Name = name;

    public string Name { get; }

    /// <summary>0 a 1.</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    public bool Muted { get; set; }

    internal float Effective => Muted ? 0f : _volume;
}
