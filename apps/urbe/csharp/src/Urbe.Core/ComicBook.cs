using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>"page2" before "page10": digits compare as numbers (of any length), the rest without case.</summary>
public sealed class NaturalOrder : IComparer<string>
{
    public static readonly NaturalOrder Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                int si = i, sj = j;
                while (i < x.Length && char.IsAsciiDigit(x[i])) i++;
                while (j < y.Length && char.IsAsciiDigit(y[j])) j++;
                var a = x.AsSpan(si, i - si).TrimStart('0');
                var b = y.AsSpan(sj, j - sj).TrimStart('0');
                if (a.Length != b.Length) return a.Length.CompareTo(b.Length);
                int digits = a.SequenceCompareTo(b);
                if (digits != 0) return digits;
                // "01" after "1", so the order is total
                if (i - si != j - sj) return (i - si).CompareTo(j - sj);
                continue;
            }
            int c = char.ToLowerInvariant(x[i]).CompareTo(char.ToLowerInvariant(y[j]));
            if (c != 0) return c;
            i++; j++;
        }
        int rest = (x.Length - i).CompareTo(y.Length - j);
        return rest != 0 ? rest : string.CompareOrdinal(x, y);
    }
}

/// <summary>
/// A comic book (CBZ: a zip of pictures) for the native reader (CST-238). The archive goes through
/// <see cref="ReaderArchive"/> (no path outside it, no duplicates, a decompression budget); only picture entries become
/// pages, in natural order, and a page is decompressed only when the reader asks for it. CBR (RAR) and CB7 (7z) are
/// not read: <see cref="NotSupportedException"/>, so the reader can say so and offer another app.
/// </summary>
public sealed partial class ComicBook : IDisposable
{
    /// <summary>The most pages one book can have here (a zip of millions of tiny entries is not a comic).</summary>
    public const int MaxPages = 20000;

    private readonly ReaderArchive _archive;
    public IReadOnlyList<string> Pages { get; }
    public int PageCount => Pages.Count;

    /// <summary>Entries that are not pages (ComicInfo.xml, thumbnails of other systems), for the reader's detail line.</summary>
    public int IgnoredEntries { get; }

    private ComicBook(ReaderArchive archive, IReadOnlyList<string> pages, int ignored)
    {
        _archive = archive;
        Pages = pages;
        IgnoredEntries = ignored;
    }

    public static ComicBook Open(byte[] bytes)
    {
        var signature = FileSignatures.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, 4096)));
        if (signature is FileSignature.Rar or FileSignature.SevenZip)
            throw new NotSupportedException("Este quadrinho é " + FileSignatures.Describe(signature) + " (CBR/CB7); o Urbe só abre CBZ. Abra em outro app ou converta para CBZ.");
        if (signature != FileSignature.Zip)
            throw new InvalidDataException("O arquivo não é um CBZ: o conteúdo parece " + FileSignatures.Describe(signature) + ".");
        var archive = new ReaderArchive(bytes);
        try
        {
            var pages = archive.Paths.Where(IsPage).Order(NaturalOrder.Instance).ToArray();
            if (pages.Length == 0) throw new InvalidDataException("O CBZ não contém imagens de página.");
            if (pages.Length > MaxPages) throw new InvalidDataException("O CBZ tem mais de " + MaxPages + " imagens; não parece um quadrinho.");
            return new ComicBook(archive, pages, archive.Paths.Count - pages.Length);
        }
        catch { archive.Dispose(); throw; }
    }

    /// <summary>A picture entry, not a system's hidden file (__MACOSX/, ._name).</summary>
    public static bool IsPage(string path) =>
        PageRe().IsMatch(path) && !path.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(path).StartsWith('.');

    /// <summary>
    /// The bytes of page <paramref name="index"/>, only when they really are a picture: a renamed entry, or a member
    /// that is not what its name says, is <see cref="InvalidDataException"/> and never reaches the decoder.
    /// </summary>
    public byte[] PageBytes(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Pages.Count);
        byte[] bytes;
        lock (_archive) bytes = _archive.Bytes(Pages[index]);
        if (!FileSignatures.IsImage(FileSignatures.Detect(bytes)))
            throw new InvalidDataException("A página " + (index + 1) + " não é uma imagem válida.");
        return bytes;
    }

    public void Dispose()
    {
        lock (_archive) _archive.Dispose();
    }

    [GeneratedRegex(@"\.(png|jpe?g|webp|gif|bmp)$", RegexOptions.IgnoreCase)] private static partial Regex PageRe();
}
