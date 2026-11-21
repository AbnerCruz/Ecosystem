using Urbe.Core.Execution;

namespace Urbe.Core.Tests;

public sealed class AssistantCodeRunnerTests
{
    private sealed class Fake : IAssistantCodeRunner
    {
        public IReadOnlyList<string> Languages => ["python"];
        public Task<CodeRunResult> RunAsync(CodeRun run, CancellationToken cancel) => Task.FromResult(new CodeRunResult("ok", "", "", 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task WithoutASandboxTheRunnerSaysSoInsteadOfPretending()
    {
        var r = await new UnavailableCodeRunner().RunAsync(new CodeRun("python", "print(1)"), CancellationToken.None);
        Assert.Equal("unavailable", r.Status);
        Assert.False(r.Ok);
        Assert.Contains("não está disponível", AssistantCodeRunners.Validate(new CodeRun("python", "print(1)"), new UnavailableCodeRunner()));
    }

    [Theory]
    [InlineData("python", "print(1)", 10, 256, null)]
    [InlineData("python", " ", 10, 256, "vazio")]
    [InlineData("ruby", "puts 1", 10, 256, "não disponível")]
    [InlineData("python", "x", 600, 256, "tempo")]
    [InlineData("python", "x", 10, 4096, "memória")]
    public void ARunIsCheckedBeforeItGoesToTheSandbox(string lang, string src, int seconds, int mb, string? error)
    {
        var why = AssistantCodeRunners.Validate(new CodeRun(lang, src, "", TimeSpan.FromSeconds(seconds), mb * 1024L * 1024), new Fake());
        if (error is null) Assert.Null(why); else Assert.Contains(error, why);
    }
}
