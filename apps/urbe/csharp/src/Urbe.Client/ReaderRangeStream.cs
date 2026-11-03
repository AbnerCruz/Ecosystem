namespace Urbe.Client;

/// <summary>One bounded response owns one read-only file handle. No book/media is copied into memory.</summary>
public sealed class ReaderRangeStream : Stream
{
    private readonly Stream _source;
    private readonly long _offset, _length;
    private long _position;
    public ReaderRangeStream(Stream source, long offset, long length)
    {
        if (!source.CanSeek || offset < 0 || length < 0 || offset > source.Length || length > source.Length - offset) throw new ArgumentOutOfRangeException(nameof(length));
        _source = source; _offset = offset; _length = length; source.Position = offset;
    }
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
    public override int Read(byte[] buffer, int offset, int count)
    { int read = _source.Read(buffer, offset, (int)Math.Min(count, _length - _position)); _position += read; return read; }
    public override int Read(Span<byte> buffer)
    { int read = _source.Read(buffer[..(int)Math.Min(buffer.Length, _length - _position)]); _position += read; return read; }
    public override long Seek(long offset, SeekOrigin origin)
    {
        long next = checked((origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? _position : _length) + offset);
        if (next < 0 || next > _length) throw new IOException("Posição fora da região autorizada.");
        _source.Position = _offset + next; return _position = next;
    }
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
}
