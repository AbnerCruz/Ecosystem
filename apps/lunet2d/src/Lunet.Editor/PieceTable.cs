using System.Text;

namespace Lunet.Editor;

/// <summary>
/// Estrutura de texto para documentos grandes: o texto original nunca é copiado nem alterado; as inserções vão para um
/// buffer só de acréscimo e o documento é uma lista de "pedaços". Inserir e apagar não movem o texto todo, e o índice de
/// linhas é mantido por pedaço. Digitação contígua estende o último pedaço em vez de criar outro.
/// </summary>
public sealed class PieceTable
{
    private readonly struct Piece(bool added, int start, int length, int lineBreaks)
    {
        public bool Added { get; } = added;
        public int Start { get; } = start;
        public int Length { get; } = length;
        public int LineBreaks { get; } = lineBreaks;
    }

    private readonly string _original;
    private readonly StringBuilder _add = new();
    private readonly List<Piece> _pieces = [];
    private int _length;
    private int _lineBreaks;

    public PieceTable(string text = "")
    {
        _original = text;
        if (text.Length > 0) _pieces.Add(new Piece(false, 0, text.Length, CountBreaks(text)));
        _length = text.Length;
        _lineBreaks = _pieces.Sum(p => p.LineBreaks);
    }

    public int Length => _length;

    /// <summary>Número de linhas (um documento vazio tem 1).</summary>
    public int LineCount => _lineBreaks + 1;

    public int PieceCount => _pieces.Count;

    public char this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            var (piece, offset) = Locate(index);
            return CharAt(_pieces[piece], offset);
        }
    }

    public void Insert(int position, string text)
    {
        if (position < 0 || position > _length) throw new ArgumentOutOfRangeException(nameof(position));
        if (text.Length == 0) return;
        var breaks = CountBreaks(text);
        var addStart = _add.Length;
        _add.Append(text);
        var inserted = new Piece(true, addStart, text.Length, breaks);

        if (_pieces.Count == 0) _pieces.Add(inserted);
        else if (position == _length)
        {
            var last = _pieces[^1];
            if (last.Added && last.Start + last.Length == addStart)
                _pieces[^1] = new Piece(true, last.Start, last.Length + text.Length, last.LineBreaks + breaks);
            else _pieces.Add(inserted);
        }
        else
        {
            var (index, offset) = Locate(position);
            var piece = _pieces[index];
            if (offset == 0) _pieces.Insert(index, inserted);
            else if (offset == piece.Length && piece.Added && piece.Start + piece.Length == addStart)
                _pieces[index] = new Piece(true, piece.Start, piece.Length + text.Length, piece.LineBreaks + breaks);
            else
            {
                var (left, right) = Split(piece, offset);
                _pieces[index] = left;
                _pieces.Insert(index + 1, inserted);
                _pieces.Insert(index + 2, right);
            }
        }
        _length += text.Length;
        _lineBreaks += breaks;
    }

    public void Delete(int position, int length)
    {
        if (position < 0 || length < 0 || position + length > _length) throw new ArgumentOutOfRangeException(nameof(length));
        if (length == 0) return;
        var removedBreaks = CountBreaks(GetText(position, length));
        var (first, firstOffset) = Locate(position);
        var (last, lastOffset) = Locate(position + length);
        var kept = new List<Piece>(2);
        if (firstOffset > 0) kept.Add(Split(_pieces[first], firstOffset).Left);
        if (lastOffset < _pieces[last].Length) kept.Add(Split(_pieces[last], lastOffset).Right);
        _pieces.RemoveRange(first, last - first + 1);
        _pieces.InsertRange(first, kept);
        _length -= length;
        _lineBreaks -= removedBreaks;
    }

    public string GetText(int start, int length)
    {
        if (start < 0 || length < 0 || start + length > _length) throw new ArgumentOutOfRangeException(nameof(length));
        var sb = new StringBuilder(length);
        if (length == 0) return "";
        var (index, offset) = Locate(start);
        var remaining = length;
        while (remaining > 0)
        {
            var piece = _pieces[index];
            var take = Math.Min(remaining, piece.Length - offset);
            sb.Append(Slice(piece, offset, take));
            remaining -= take;
            index++;
            offset = 0;
        }
        return sb.ToString();
    }

    public override string ToString() => GetText(0, _length);

    /// <summary>Texto da linha (0 = primeira), sem a quebra final.</summary>
    public string GetLine(int line)
    {
        if ((uint)line >= (uint)LineCount) throw new ArgumentOutOfRangeException(nameof(line));
        var start = LineStart(line);
        var end = line + 1 < LineCount ? LineStart(line + 1) - 1 : _length;
        return GetText(start, end - start);
    }

    /// <summary>Posição do primeiro caractere da linha.</summary>
    public int LineStart(int line)
    {
        if ((uint)line >= (uint)LineCount) throw new ArgumentOutOfRangeException(nameof(line));
        if (line == 0) return 0;
        var remaining = line;
        var position = 0;
        foreach (var piece in _pieces)
        {
            if (piece.LineBreaks < remaining)
            {
                remaining -= piece.LineBreaks;
                position += piece.Length;
                continue;
            }
            var span = Span(piece);
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] != '\n') continue;
                if (--remaining == 0) return position + i + 1;
            }
            position += piece.Length;
        }
        return _length;
    }

    /// <summary>Linha (0 = primeira) que contém a posição.</summary>
    public int LineOf(int position)
    {
        if (position < 0 || position > _length) throw new ArgumentOutOfRangeException(nameof(position));
        var line = 0;
        var offset = 0;
        foreach (var piece in _pieces)
        {
            if (position >= offset + piece.Length)
            {
                line += piece.LineBreaks;
                offset += piece.Length;
                continue;
            }
            var span = Span(piece)[..(position - offset)];
            foreach (var c in span) if (c == '\n') line++;
            return line;
        }
        return line;
    }

    private (int Piece, int Offset) Locate(int position)
    {
        var offset = 0;
        for (var i = 0; i < _pieces.Count; i++)
        {
            var length = _pieces[i].Length;
            if (position < offset + length || (position == offset + length && i == _pieces.Count - 1)) return (i, position - offset);
            offset += length;
        }
        return (Math.Max(0, _pieces.Count - 1), _pieces.Count == 0 ? 0 : _pieces[^1].Length);
    }

    private (Piece Left, Piece Right) Split(Piece piece, int offset)
    {
        var leftBreaks = CountBreaks(Slice(piece, 0, offset));
        return (new Piece(piece.Added, piece.Start, offset, leftBreaks),
            new Piece(piece.Added, piece.Start + offset, piece.Length - offset, piece.LineBreaks - leftBreaks));
    }

    // StringBuilder não expõe um span contíguo; o trecho inserido é copiado (pedaços de inserção são pequenos).
    private ReadOnlySpan<char> Span(Piece piece) => piece.Added
        ? _add.ToString(piece.Start, piece.Length).AsSpan()
        : _original.AsSpan(piece.Start, piece.Length);

    private string Slice(Piece piece, int offset, int length) => piece.Added
        ? _add.ToString(piece.Start + offset, length)
        : _original.Substring(piece.Start + offset, length);

    private char CharAt(Piece piece, int offset) => piece.Added ? _add[piece.Start + offset] : _original[piece.Start + offset];

    private static int CountBreaks(string text)
    {
        var count = 0;
        foreach (var c in text) if (c == '\n') count++;
        return count;
    }
}
