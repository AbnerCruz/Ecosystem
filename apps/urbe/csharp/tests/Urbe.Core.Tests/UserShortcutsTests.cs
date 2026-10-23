namespace Urbe.Core.Tests;

/// <summary>UC-61: the person's shortcuts in Personalização/atalhos.json, over the 1.8.4's.</summary>
public sealed class UserShortcutsTests
{
    [Fact]
    public void ShortcutsAreWrittenAsTheKeymapReadsThem()
    {
        Assert.Equal("Mod+Shift+K", UserShortcuts.Canonical("shift+ctrl+k"));
        Assert.Equal("Mod+Alt+ArrowLeft", UserShortcuts.Canonical("Alt + Mod + ArrowLeft"));
        Assert.Null(UserShortcuts.Canonical("Hyper+K"));
        var map = UserShortcuts.Parse("""{"versao":1,"atalhos":{"ctrl+shift+k":"ui.constellation","Mod+N":"","Hyper+X":"a"}}""");
        Assert.Equal(new Dictionary<string, string> { ["Mod+Shift+K"] = "ui.constellation", ["Mod+N"] = "" }, map);
        Assert.Empty(UserShortcuts.Parse("{ não é json"));
        Assert.Equal(3, UserShortcuts.Parse(UserShortcuts.Template).Count);
    }

    [Fact]
    public void MineComeFirstAnEmptyOneTurnsADefaultOffAndBareKeysNeverRunWhileTyping()
    {
        static bool Has(string _) => true;
        var mine = UserShortcuts.Parse("""{"versao":1,"atalhos":{"Mod+Shift+K":"ui.constellation","Mod+N":"","G":"ui.workspace"}}""");
        Assert.Equal("ui.constellation", LegacyKeymap.Resolve(new LegacyKeyInput("k", Ctrl: true, Shift: true), true, Has, mine).CommandId);
        Assert.Null(LegacyKeymap.Resolve(new LegacyKeyInput("n", Ctrl: true), false, Has, mine).CommandId);
        Assert.Equal("document.create", LegacyKeymap.Resolve(new LegacyKeyInput("n", Ctrl: true), false, Has, null).CommandId);
        Assert.Null(LegacyKeymap.Resolve(new LegacyKeyInput("g"), true, Has, mine).CommandId);
        Assert.Equal("ui.workspace", LegacyKeymap.Resolve(new LegacyKeyInput("g"), false, Has, mine).CommandId);
        // the rest of the defaults stay
        Assert.Equal("ui.commandPalette.open", LegacyKeymap.Resolve(new LegacyKeyInput("k", Ctrl: true), true, Has, mine).CommandId);
    }
}
