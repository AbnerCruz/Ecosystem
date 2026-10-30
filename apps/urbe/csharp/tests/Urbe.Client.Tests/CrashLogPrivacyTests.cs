using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-097: the error log keeps the error, not the person's content, and only the last 30 days.</summary>
public sealed class CrashLogPrivacyTests
{
    [Fact]
    public void SecretsQuotedTextAndPathsAreTakenOut()
    {
        var error = "HttpRequestException: 401 for https://api.example.com/v1?api_key=abc123secret&x=1 (Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload) " +
                    "sk-or-v1-0123456789abcdef. Já existe \"Diário/A reunião com o advogado é na quinta às 15h, sala 4.md\". " +
                    "at /storage/emulated/0/Documents/Cofre/Pessoal/segredos.md and C:\\Users\\Abner\\Documents\\Cofre\\nota.md";
        var shown = CrashLog.Private(error);
        Assert.DoesNotContain("abc123secret", shown);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", shown);
        Assert.DoesNotContain("0123456789abcdef", shown);
        Assert.DoesNotContain("advogado", shown);
        Assert.DoesNotContain("Pessoal", shown);
        Assert.DoesNotContain("Abner", shown);
        Assert.Contains("api_key=…", shown);
        Assert.Contains("caracteres", shown);
        Assert.Contains("…/segredos.md", shown);
        Assert.Contains("HttpRequestException: 401", shown);   // the error itself stays
        Assert.Equal("NullReferenceException at Urbe.Client.MainView.Open()", CrashLog.Private("NullReferenceException at Urbe.Client.MainView.Open()"));
    }

    [Fact]
    public void EntriesOlderThanThirtyDaysLeave()
    {
        var log = "== 2026-09-01 10:00:00 · interface\nvelho\n\n== 2026-10-20 09:00:00 · tarefa\nrecente\n\n";
        var kept = CrashLog.Recent(log, new DateTime(2026, 10, 30));
        Assert.DoesNotContain("velho", kept);
        Assert.Contains("recente", kept);
    }
}
