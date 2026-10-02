namespace Hub.Core;

/// <summary>Repositório do GitHub, derivado da URL declarada em <c>ecosystem.json</c> — nunca digitado no código.</summary>
public sealed record RepositoryRef(string Owner, string Name)
{
    public string FullName => $"{Owner}/{Name}";

    public static RepositoryRef? TryParse(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) || !u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = u.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        var name = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        return name.Length == 0 ? null : new RepositoryRef(parts[0], name);
    }
}

public sealed record PullRequestInfo(int Number, string Title, bool Draft, string HeadRef, string Url);

public sealed record BranchInfo(string Name);

public sealed record ReleaseInfo(string Tag, string? Name, bool Prerelease, string? PublishedAt, string Url, int Assets);

/// <param name="State">Valor da label <c>state:&lt;x&gt;</c> (DEC-0003), quando houver.</param>
public sealed record IssueInfo(int Number, string Title, string Url, IReadOnlyList<string> Labels, string? State);

public sealed record WorkflowRunInfo(string Name, string Status, string? Conclusion, string Branch, string Sha, string Url);

/// <summary>Leitura conjunta do GitHub. Cada parte degrada sozinha para <see cref="Availability.NotAvailable"/>.</summary>
public sealed record GitHubSnapshot(
    RepositoryRef Repository,
    Datum<IReadOnlyList<PullRequestInfo>> PullRequests,
    Datum<IReadOnlyList<BranchInfo>> Branches,
    Datum<IReadOnlyList<ReleaseInfo>> Releases,
    Datum<IReadOnlyList<IssueInfo>> Issues,
    Datum<IReadOnlyList<WorkflowRunInfo>> CiRuns);
