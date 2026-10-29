namespace Urbe.Core;

/// <summary>The pixel width of a picture read from its header, without decoding it (PNG, JPEG, GIF, WebP, BMP); null when unknown.</summary>
public static class ImageHeader
{
    public static int? Width(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 24 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G')
            return (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
        if (b.Length >= 10 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F')
            return b[6] | (b[7] << 8);
        if (b.Length >= 26 && b[0] == 'B' && b[1] == 'M')
            return Math.Abs(b[18] | (b[19] << 8) | (b[20] << 16) | (b[21] << 24));
        if (b.Length >= 30 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == ' ') return (b[26] | (b[27] << 8)) & 0x3FFF;
            if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == 'L') return 1 + (b[21] | ((b[22] & 0x3F) << 8));
            if (b[12] == 'V' && b[13] == 'P' && b[14] == '8' && b[15] == 'X') return 1 + (b[24] | (b[25] << 8) | (b[26] << 16));
            return null;
        }
        if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xD8)
        {
            int i = 2;
            while (i + 9 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                byte marker = b[i + 1];
                if (marker == 0xFF) { i++; continue; }
                if (marker is 0xD8 or 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
                int length = (b[i + 2] << 8) | b[i + 3];
                // start of frame (any SOFn but the DHT, JPG and DAC markers): height then width
                if (marker >= 0xC0 && marker <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
                    return (b[i + 7] << 8) | b[i + 8];
                if (length < 2) return null;
                i += 2 + length;
            }
        }
        return null;
    }
}
