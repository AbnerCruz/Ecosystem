using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-072: the permission matrix over the whole catalogue. A tool whose name says it changes something is classified
/// as a change, and a read-only agent (or a read-only policy) is never given one — so a prohibition cannot be
/// walked around through another tool that was filed as "read".
/// </summary>
public sealed class ToolPermissionMatrixTests
{
    private static readonly string[] Changing = ["write_", "edit_", "create_", "rename_", "delete", "move_", "append_", "add_", "generate_", "set_appearance", "trash", "restore"];

    [Fact]
    public void EveryToolThatChangesTheVaultIsFiledAsAChange()
    {
        var wrong = AssistantTools.All
            .Where(t => Changing.Any(p => t.Name.StartsWith(p, StringComparison.Ordinal)))
            .Where(t => t.Access is "read" or "ui" or "plan" or "network" or "memory")
            .Select(t => t.Name + " (" + t.Access + ")").ToList();
        Assert.Empty(wrong);
    }

    [Fact]
    public void AReadOnlyAgentOrPolicyGetsNoToolThatWrites()
    {
        var reader = AssistantAgent.Agents.Single(a => a.Id == "pesquisador");
        var writer = AssistantAgent.Agents.Single(a => a.Id == "geral");
        foreach (var tool in AssistantTools.All)
        {
            var writes = tool.Access is "write" or "destructive" or "plugin";
            if (!writes) continue;
            Assert.False(AssistantAgent.AllowTool(reader, "ask", tool.Access), tool.Name);
            Assert.False(AssistantAgent.AllowTool(writer, "readonly", tool.Access), tool.Name);
        }
        // the escritor writes but never destroys
        var escritor = AssistantAgent.Agents.Single(a => a.Id == "escritor");
        Assert.DoesNotContain(AssistantTools.All, t => t.Access == "destructive" && AssistantAgent.AllowTool(escritor, "ask", t.Access));
    }
}
