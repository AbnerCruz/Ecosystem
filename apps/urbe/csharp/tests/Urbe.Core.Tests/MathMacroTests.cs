using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class MathMacroTests
{
    [Fact]
    public void EmptyMacroSetIsIdentityAndPreservesSource()
    {
        const string tex = @"x + \frac{1}{2}";

        var result = MathMacros.Expand(tex, null);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal(tex, result.Source);
        Assert.Equal(tex, result.Expanded);
    }

    [Fact]
    public void ExpandsConstantAndParameterizedStringMacros()
    {
        const string tex = @"\RR + \sq{x+1} + \pair{a}{b}";
        var macros = new Dictionary<string, string>
        {
            [@"\RR"] = @"\mathbb{R}",
            [@"\sq"] = @"\left(#1\right)^2",
            [@"\pair"] = @"\left(#1,#2\right)"
        };

        var result = MathMacros.Expand(tex, macros);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal(tex, result.Source);
        Assert.Equal(
            @"\mathbb{R} + \left(x+1\right)^2 + \left(a,b\right)",
            result.Expanded);
    }

    [Fact]
    public void SupportsNestedMacrosAndNestedArgumentGroups()
    {
        const string tex = @"\outer{{a+b}^{2}}";
        var macros = new Dictionary<string, string>
        {
            [@"\inner"] = @"\sqrt{#1}",
            [@"\outer"] = @"\inner{#1}"
        };

        var result = MathMacros.Expand(tex, macros);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal(@"\sqrt{{a+b}^{2}}", result.Expanded);
    }

    [Fact]
    public void SupportsSingleTokenArgumentsLikeTex()
    {
        var macros = new Dictionary<string, string>
        {
            [@"\sq"] = @"{#1}^{2}"
        };

        var letter = MathMacros.Expand(@"\sq x", macros);
        var command = MathMacros.Expand(@"\sq \alpha", macros);

        Assert.True(letter.Success, letter.Diagnostic);
        Assert.Equal(@"{x}^{2}", letter.Expanded);
        Assert.True(command.Success, command.Diagnostic);
        Assert.Equal(@"{\alpha}^{2}", command.Expanded);
    }

    [Fact]
    public void CommentsArePreservedAndNotExpanded()
    {
        const string tex = "\\RR % \\RR ignored\n+ \\RR";
        var macros = new Dictionary<string, string> { [@"\RR"] = @"\mathbb{R}" };

        var result = MathMacros.Expand(tex, macros);

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal("\\mathbb{R} % \\RR ignored\n+ \\mathbb{R}", result.Expanded);
    }

    [Fact]
    public void DetectsCyclesWithoutMutatingSource()
    {
        const string tex = @"\a";
        var macros = new Dictionary<string, string>
        {
            [@"\a"] = @"\b",
            [@"\b"] = @"\a"
        };

        var result = MathMacros.Expand(tex, macros);

        Assert.False(result.Success);
        Assert.Equal(tex, result.Source);
        Assert.Equal(tex, result.Expanded);
        Assert.Contains("Ciclo", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsInvalidDefinitionsAndMissingArguments()
    {
        var invalidName = MathMacros.Expand(
            @"\ok",
            new Dictionary<string, string> { ["ok"] = "x" });
        Assert.False(invalidName.Success);
        Assert.Contains("Nome", invalidName.Diagnostic, StringComparison.OrdinalIgnoreCase);

        var invalidPlaceholder = MathMacros.Expand(
            @"\m{x}",
            new Dictionary<string, string> { [@"\m"] = "#0" });
        Assert.False(invalidPlaceholder.Success);
        Assert.Contains("#1", invalidPlaceholder.Diagnostic, StringComparison.Ordinal);

        var missing = MathMacros.Expand(
            @"\m",
            new Dictionary<string, string> { [@"\m"] = "#1" });
        Assert.False(missing.Success);
        Assert.Contains("argumento #1", missing.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExpansionIsBoundedAgainstExplosion()
    {
        var macros = new Dictionary<string, string>
        {
            [@"\double"] = "#1#1"
        };
        var tex = @"\double{\double{\double{\double{x}}}}";

        var safe = MathMacros.Expand(tex, macros);
        Assert.True(safe.Success, safe.Diagnostic);
        Assert.Equal(new string('x', 16), safe.Expanded);

        var tooMany = Enumerable.Range(0, MathMacros.MaxDefinitions + 1)
            .ToDictionary(index => @"\m" + ToLetters(index), _ => "x");
        var rejected = MathMacros.Expand("x", tooMany);
        Assert.False(rejected.Success);
        Assert.Contains("Quantidade", rejected.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LiteralHashEscapingIsDeterministic()
    {
        var result = MathMacros.Expand(
            @"\hash{x}",
            new Dictionary<string, string> { [@"\hash"] = @"## #1" });

        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal("# x", result.Expanded);
    }

    private static string ToLetters(int value)
    {
        var number = value;
        var chars = new List<char>();
        do
        {
            chars.Add((char)('a' + number % 26));
            number /= 26;
        } while (number > 0);
        chars.Reverse();
        return new string(chars.ToArray());
    }
}
