using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-144: typed text not yet saved survives a crash, per vault and note, and leaves once saved.</summary>
public sealed class NoteDraftStoreTests
{
    [Fact]
    public void ADraftIsKeptPerVaultAndNoteUntilCleared()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-drafts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NoteDraftStore(dir);
            var at = new DateTimeOffset(2026, 11, 17, 9, 30, 0, TimeSpan.FromHours(-3));
            store.Save(new NoteDraft("vault-a", "Notas/Plano.md", "# Plano\n", "# Plano\n\nTexto novo, ação.\n", at));
            store.Save(new NoteDraft("vault-b", "Notas/Plano.md", "outro", "outro vault", at));
            var read = new NoteDraftStore(dir).Get("vault-a", "Notas/Plano.md")!;   // a new process reads it
            Assert.Equal("# Plano\n\nTexto novo, ação.\n", read.Text);
            Assert.Equal("# Plano\n", read.Saved);
            Assert.Equal(at, read.At);
            Assert.Equal("outro vault", store.Get("vault-b", "Notas/Plano.md")!.Text);
            Assert.Null(store.Get("vault-a", "Outra.md"));
            Assert.True(NoteDraftStore.Offers(read, "# Plano\n"));
            Assert.False(NoteDraftStore.Offers(read, read.Text));   // already in the note: nothing to offer
            Assert.False(NoteDraftStore.Changed(read, "# Plano\n"));
            Assert.True(NoteDraftStore.Changed(read, "# Plano mudado em outro lugar\n"));
            store.Clear("vault-a", "Notas/Plano.md");
            Assert.Null(store.Get("vault-a", "Notas/Plano.md"));
            Assert.NotNull(store.Get("vault-b", "Notas/Plano.md"));
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void AnUnreadableDraftIsNotOfferedAndNotDeleted()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-drafts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new NoteDraftStore(dir);
            store.Save(new NoteDraft("v", "A.md", "", "x", DateTimeOffset.UtcNow));
            var file = Directory.GetFiles(dir).Single();
            File.WriteAllText(file, "{quebrado");
            Assert.Null(store.Get("v", "A.md"));
            Assert.True(File.Exists(file));
        }
        finally { Directory.Delete(dir, true); }
    }
}
