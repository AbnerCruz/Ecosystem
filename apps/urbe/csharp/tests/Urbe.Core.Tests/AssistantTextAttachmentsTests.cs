using System.Text;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

public sealed class AssistantTextAttachmentsTests
{
    [Theory]
    [InlineData("pagina.html", "<!doctype html><html><body>Olá</body></html>")]
    [InlineData("component.tsx", "export const A = () => <p>Oi</p>;")]
    [InlineData("config.toml", "[section]\nkey = 'value'")]
    [InlineData("site.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("projeto.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"/>")]
    [InlineData("query.sql", "select * from notas;")]
    [InlineData("dados.jsonl", "{\"um\": 1}\n{\"dois\": 2}")]
    [InlineData("sem-extensao", "texto UTF-8 legível.")]
    [InlineData("surpresa.abc", "Este arquivo é texto apesar da extensão.")]
    public void CommonAndUnknownPlainTextFilesAreReadAsInertContext(string name, string content)
    {
        Assert.True(AssistantTextAttachments.TryDecode(name, Encoding.UTF8.GetBytes(content), out var parsed, out var error), error);
        Assert.Equal(content, parsed);
    }

    [Fact]
    public void HtmlLargerThanLegacy96KiBIsAccepted()
    {
        var html = "<html>" + new string('a', 150_000) + "</html>";
        Assert.True(AssistantTextAttachments.TryDecode("pagina.html", Encoding.UTF8.GetBytes(html), out var text, out var error), error);
        Assert.Equal(html, text);
    }

    [Fact]
    public void EncodingsWithBomAreDecodedCorrectly()
    {
        var text = "<h1>ação</h1>";
        foreach (var encoding in new Encoding[] { new UTF8Encoding(true), Encoding.Unicode, Encoding.BigEndianUnicode })
        {
            var raw = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            Assert.True(AssistantTextAttachments.TryDecode("site.html", raw, out var decoded, out var error), error);
            Assert.Equal(text, decoded);
        }
    }

    [Theory]
    [InlineData("payload.zip", new byte[] { 0x50, 0x4b, 0x03, 0x04 })]
    [InlineData("fake.html", new byte[] { 0x00, 0x01, 0x02 })]
    [InlineData("bin.dat", new byte[] { 0xff, 0xd8, 0xff })]
    [InlineData("fake.txt", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2d })]
    public void BinaryOrNonTextContentIsRejected(string name, byte[] bytes)
    {
        Assert.False(AssistantTextAttachments.TryDecode(name, bytes, out _, out var reason));
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void OversizeTextIsRejectedWithAnActionableReason()
    {
        var payload = Encoding.UTF8.GetBytes(new string('a', AssistantTextAttachments.MaxTextBytes + 1));
        Assert.False(AssistantTextAttachments.TryDecode("enorme.html", payload, out _, out var reason));
        Assert.Contains("KB", reason);
    }

    [Fact]
    public async Task BoundedReadingSupportsNonSeekableStreamsAndRejectsOversize()
    {
        byte[] data = Encoding.UTF8.GetBytes("<div>Teste</div>");
        await using var stream = new ChunkedStream(data);
        var result = await AssistantTextAttachments.ReadBoundedAsync(stream, AssistantTextAttachments.MaxTextBytes, TestContext.Current.CancellationToken);
        Assert.Equal(data, result);

        await using var huge = new ChunkedStream(new byte[AssistantTextAttachments.MaxTextBytes + 1]);
        Assert.Null(await AssistantTextAttachments.ReadBoundedAsync(huge, AssistantTextAttachments.MaxTextBytes, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TextAttachmentsAreExplicitlyMarkedAsUntrustedUserContext()
    {
        var block = AssistantTextAttachments.ToChatBlock("pagina.html", "<main>Olá</main>");
        Assert.True(block.Hidden);
        Assert.Equal("text", block.Type);
        Assert.Contains("pagina.html", block.Text);
        Assert.Contains("conteúdo não confiável", block.Text);
        Assert.Contains("<main>Olá</main>", block.Text);
        var restored = AiChatBlock.FromJson(block.ToJson());
        Assert.Equal(block.Text, restored.Text);
        Assert.True(restored.Hidden);
    }

    private sealed class ChunkedStream(byte[] data) : Stream
    {
        private int _at;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = Math.Min(Math.Min(5, count), data.Length - _at);
            Array.Copy(data, _at, buffer, offset, n);
            _at += n;
            return n;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int n = Math.Min(Math.Min(5, buffer.Length), data.Length - _at);
            data.AsMemory(_at, n).CopyTo(buffer);
            _at += n;
            return ValueTask.FromResult(n);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
