namespace Lunet.Content;

/// <summary>Image selection supplied by the platform host. Games never request broad filesystem permissions.</summary>
/// <remarks>The call does not block the game loop. Poll TryReceive on Update; null pixels and error means cancellation.</remarks>
public interface IGameImagePicker
{
    /// <summary>Whether this host can open a native image picker.</summary>
    bool CanPick { get; }

    /// <summary>Opens the platform image picker. A request already running is ignored.</summary>
    void Pick();

    /// <summary>Consumes at most one pending result, including cancellation and error.</summary>
    bool TryReceive(out PickedGameImage result);
}

/// <summary>Decoded, bounded RGBA image; the GL thread creates the texture with Texture2D.FromPixels.</summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Rgba">R,G,B,A bytes, scanline order; null for cancellation/error.</param>
/// <param name="Error">User-facing error description, or null for success/cancellation.</param>
public sealed record PickedGameImage(int Width, int Height, byte[]? Rgba, string? Error = null);
