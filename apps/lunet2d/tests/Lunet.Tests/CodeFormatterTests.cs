using Lunet.Editor;

namespace Lunet.Tests;

public class CodeFormatterTests
{
    [Fact]
    public void Reindents_ByBraceDepth_TrimsTrailingSpaces_AndEndsWithNewline()
    {
        var input = "using System;\nclass A\n{\nvoid M()\n{\nif (true)\n{\nint x = 1;   \n}\n}\n}";
        var expected = "using System;\nclass A\n{\n    void M()\n    {\n        if (true)\n        {\n            int x = 1;\n        }\n    }\n}\n";
        Assert.Equal(expected, CodeFormatter.Format(input));
    }

    [Fact]
    public void ContinuationLines_EmbeddedStatements_AndSwitchSections()
    {
        var input = "class A {\nvoid M(int a)\n{\nif (a > 0)\nreturn;\nvar s = Foo(a,\na)\n.Bar();\nswitch (a)\n{\ncase 1:\nreturn;\ndefault:\nbreak;\n}\n}\n}";
        var expected = string.Join('\n',
            "class A {",
            "    void M(int a)",
            "    {",
            "        if (a > 0)",
            "            return;",
            "        var s = Foo(a,",
            "            a)",
            "            .Bar();",
            "        switch (a)",
            "        {",
            "            case 1:",
            "                return;",
            "            default:",
            "                break;",
            "        }",
            "    }",
            "}") + "\n";
        Assert.Equal(expected, CodeFormatter.Format(input));
    }

    [Fact]
    public void Comments_Attributes_Enums_AndInitializers()
    {
        var input = "enum E\n{\nA,\nB,\n}\n[Obsolete]\nclass C\n{\n// nota\nint[] v = new[]\n{\n1,\n2,\n};\n// fim\n}";
        var expected = string.Join('\n',
            "enum E",
            "{",
            "    A,",
            "    B,",
            "}",
            "[Obsolete]",
            "class C",
            "{",
            "    // nota",
            "    int[] v = new[]",
            "    {",
            "        1,",
            "        2,",
            "    };",
            "    // fim",
            "}") + "\n";
        Assert.Equal(expected, CodeFormatter.Format(input));
    }

    [Fact]
    public void MultiLineStringsAndBlockComments_AreNotBroken()
    {
        var input = "class A\n{\nstring s = \"\"\"\n  keep\n     this\n  \"\"\";\n/* a\n   b */\nstring v = @\"x\n   y\";\n}";
        var output = CodeFormatter.Format(input);
        Assert.Contains("\n  keep\n     this\n  \"\"\";", output);
        Assert.Contains("x\n   y\";", output);
        Assert.Contains("    /* a\n       b */", output);
    }

    [Fact]
    public void IsIdempotent_AndLeavesUnbalancedCodeAlone()
    {
        var input = "class A{void M(){\nvar x=1;\n}}";
        var once = CodeFormatter.Format(input);
        Assert.Equal(once, CodeFormatter.Format(once));
        var broken = "class A {\nvoid M() {\n";
        Assert.Equal(broken, CodeFormatter.Format(broken));
    }

    [Fact]
    public void Formats_TheFrameworkDemoTemplates_WithoutChangingTheirMeaning()
    {
        var source = Lunet.Core.ProjectTemplates.CoinCatcherSource("CoinCatcher");
        var formatted = CodeFormatter.Format(source);
        var a = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken).DescendantTokens().Select(t => t.Text);
        var b = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(formatted, cancellationToken: TestContext.Current.CancellationToken).GetRoot(TestContext.Current.CancellationToken).DescendantTokens().Select(t => t.Text);
        Assert.Equal(a, b);
        Assert.Equal(formatted, CodeFormatter.Format(formatted));
    }
}
