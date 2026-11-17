using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-233: a PDF that needs a password is recognized, and the Assistente is told so instead of a generic failure.</summary>
public sealed class PdfLockTests
{
    private static byte[] Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tests", "fixtures", "readers", name))) dir = dir.Parent;
        return File.ReadAllBytes(Path.Combine(dir!.FullName, "tests", "fixtures", "readers", name));
    }

    [Fact]
    public void ALockedPdfIsToldApartFromAnOpenOne()
    {
        Assert.True(PdfLock.IsLocked(Fixture("protected.pdf")));
        Assert.False(PdfLock.IsLocked(Fixture("book-1.pdf")));
        Assert.False(PdfLock.IsLocked([1, 2, 3]));   // not a PDF: another problem, told by the reader
    }
}
