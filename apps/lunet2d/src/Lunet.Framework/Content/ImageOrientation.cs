namespace Lunet.Content;

/// <summary>
/// Applies an EXIF orientation (1–8) to an RGBA image so photos taken with the camera appear upright. Hosts decode
/// the raw pixels (which ignore EXIF) and call this before handing the image to the game. Values outside 2–8 are
/// treated as 1 (already upright).
/// </summary>
internal static class ImageOrientation
{
    public static (int Width, int Height, byte[] Rgba) Apply(int width, int height, byte[] rgba, int exifOrientation)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Dimensões não correspondem aos pixels.", nameof(rgba));
        if (exifOrientation is < 2 or > 8) return (width, height, rgba);

        bool swap = exifOrientation >= 5; // 5–8 rotate by 90°: width and height trade places
        int outWidth = swap ? height : width, outHeight = swap ? width : height;
        var result = new byte[rgba.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Destination of source pixel (x, y), per the EXIF orientation table.
                var (dx, dy) = exifOrientation switch
                {
                    2 => (width - 1 - x, y),                 // mirror horizontal
                    3 => (width - 1 - x, height - 1 - y),    // rotate 180
                    4 => (x, height - 1 - y),                // mirror vertical
                    5 => (y, x),                             // transpose
                    6 => (height - 1 - y, x),                // rotate 90 clockwise
                    7 => (height - 1 - y, width - 1 - x),    // transverse
                    _ => (y, width - 1 - x),                 // 8: rotate 270 clockwise
                };
                Buffer.BlockCopy(rgba, (y * width + x) * 4, result, (dy * outWidth + dx) * 4, 4);
            }
        }
        return (outWidth, outHeight, result);
    }
}
