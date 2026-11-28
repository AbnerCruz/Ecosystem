using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>
/// The Studios' journaled save over <see cref="IProjectFiles"/> (F3, files edited in the game's own folder): an app that
/// stopped after writing a file but before marking it is rolled back as a whole on the next opening — no bundle left
/// half new, half old.
/// </summary>
public sealed class StudioRecoveryStorageTests
{
    private static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b));

    [Fact]
    public void AStopAfterWritingButBeforeMarking_IsRolledBackOnTheNextOpening()
    {
        var root = Directory.CreateTempSubdirectory("lunet-studio-rec-").FullName;
        try
        {
            var files = new DiskProjectFiles(root);
            var oldSprite = Encoding.UTF8.GetBytes("{\"old\":1}");
            var newSprite = Encoding.UTF8.GetBytes("{\"new\":2}");
            var newPng = new byte[] { 1, 2, 3 };
            files.Write("Assets/heroi.sprite.json", newSprite);                       // written…
            var stage = StudioAssetWorkspace.RecoveryFolder + "/stage-" + Guid.NewGuid().ToString("N");
            files.Write(stage + "/0.next", newSprite);                                  // …but its .next not yet removed
            files.Write(stage + "/0.before", oldSprite);
            files.Write(stage + "/1.next", newPng);                                     // the second file never written
            files.Write(stage + "/journal.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object[]
            {
                new { Relative = "Assets/heroi.sprite.json", Before = Hash(oldSprite), After = Hash(newSprite) },
                new { Relative = "Assets/heroi.png", Before = (string?)null, After = Hash(newPng) },
            })));

            var studio = new StudioAssetWorkspace(files);                               // the next opening recovers
            Assert.Equal(oldSprite, files.Read("Assets/heroi.sprite.json"));            // back to the old drawing
            Assert.False(files.FileExists("Assets/heroi.png"));                         // nothing half-made
            Assert.False(files.DirectoryExists(stage));

            studio.WriteBundle(("Assets/heroi.sprite.json", newSprite), ("Assets/heroi.png", newPng)); // a normal save
            Assert.Equal(newSprite, files.Read("Assets/heroi.sprite.json"));
            Assert.Equal(newPng, files.Read("Assets/heroi.png"));
            Assert.Empty(files.List(StudioAssetWorkspace.RecoveryFolder));
        }
        finally { Directory.Delete(root, true); }
    }
}
