using System.Globalization;

namespace Urbe.Core;

public sealed record ReaderByteRange(long Offset, long Count, bool Partial)
{
    public static ReaderByteRange? Parse(string? header, long size)
    {
        if (size < 0) return null;
        if (string.IsNullOrEmpty(header)) return new(0, size, false);
        if (!header.StartsWith("bytes=", StringComparison.Ordinal) || size == 0) return null;
        var parts = header[6..].Split('-');
        if (parts.Length != 2) return null;
        bool Number(string s, out long n) => long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out n);
        if (parts[0].Length == 0)
        {
            if (!Number(parts[1], out long suffix) || suffix == 0) return null;
            long count = Math.Min(size, suffix); return new(size - count, count, true);
        }
        if (!Number(parts[0], out long start) || start >= size) return null;
        long end = size - 1;
        if (parts[1].Length > 0 && (!Number(parts[1], out end) || end < start)) return null;
        end = Math.Min(size - 1, end);
        return new(start, end - start + 1, true);
    }
}
