using System.Text.Json;

namespace Urbe.Core;

/// <summary>Read-only projection of the camera from the original 1.8.4
/// .urbe/mapa.json. The original file remains the sole source of truth.
/// A corrupt or future map is never overwritten or "repaired".</summary>
public static class LegacySavedMapCamera
{
    public static bool TryRead(VaultSnapshot snapshot,
        out (double X, double Y, double Zoom) camera)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        camera = default;
        if (snapshot.IsMapReadOnly ||
            !snapshot.Files.TryGetValue(".urbe/mapa.json", out var file))
            return false;
        try
        {
            using var json = JsonDocument.Parse(file.Bytes);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("v", out var version) ||
                !version.TryGetInt32(out var v) || v is < 1 or > 4 ||
                !root.TryGetProperty("camera", out var c) ||
                c.ValueKind != JsonValueKind.Object ||
                !c.TryGetProperty("x", out var x) || !x.TryGetDouble(out var px) ||
                !c.TryGetProperty("y", out var y) || !y.TryGetDouble(out var py) ||
                !c.TryGetProperty("z", out var z) || !z.TryGetDouble(out var zoom) ||
                !double.IsFinite(px) || !double.IsFinite(py) ||
                !double.IsFinite(zoom) ||
                Math.Abs(px) > 32_000_000 || Math.Abs(py) > 32_000_000 ||
                zoom < .22 || zoom > 2.8)
                return false;

            camera = (px, py, zoom);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
