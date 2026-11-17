using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-140: what would fail silently in atalhos.json is said, line by line.</summary>
public sealed class UserShortcutProblemsTests
{
    private static bool Known(string id) => id is "ui.workspace" or "ui.constellation";

    [Fact]
    public void DuplicatesUnknownCommandsBadKeysAndUnreadableFilesAreTold()
    {
        var problems = UserShortcuts.Problems("""
            {"versao": 1, "atalhos": {"Ctrl+B": "ui.workspace", "Mod+B": "ui.constellation", "Mod+Shift+K": "ui.cosntellation", "Hiper+Q": "ui.workspace", "Mod+N": ""}}
            """, Known);
        Assert.Contains(problems, p => p.Contains("“Ctrl+B” e “Mod+B” são a mesma tecla"));
        Assert.Contains(problems, p => p.Contains("ui.cosntellation") && p.Contains("não existe"));
        Assert.Contains(problems, p => p.Contains("“Hiper+Q” não é um atalho"));
        Assert.Equal(3, problems.Count);   // "Mod+N": "" turns a default off on purpose: not a problem
        Assert.Contains("não pôde ser lido", Assert.Single(UserShortcuts.Problems("{\"atalhos\": {", Known)));
        Assert.Empty(UserShortcuts.Problems(UserShortcuts.Template, _ => true));
        Assert.Empty(UserShortcuts.Problems(null, Known));
    }
}
