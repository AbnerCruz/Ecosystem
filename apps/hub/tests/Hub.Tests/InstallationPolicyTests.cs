using Hub.Core;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hub.Tests;

public sealed class InstallationPolicyTests
{
    static readonly string Cert = new('A', 64);
    static readonly byte[] Bytes = Enumerable.Range(0, 160000).Select(i => (byte)(i % 251)).ToArray();
    static readonly string Hash = Convert.ToHexString(SHA256.HashData(Bytes));
    static AndroidProductTrust Trust => new("alpha", "io.alpha.app", "acme/catalog", "alpha-v", Cert, "DEC-0040",
        $"A — alpha io.alpha.app acme/catalog alpha-v {Cert}", "apps/alpha/build", true);
    static ArtifactChoice Choice => new("alpha", "Alpha", new(new("acme", "catalog"), "https://github.com/acme/catalog/releases", "alpha-v"), "alpha-v2",
        new("alpha.apk", "https://github.com/acme/catalog/releases/download/alpha-v2/alpha.apk", Bytes.Length, Hash), false);
    static AndroidPackageEvidence Package(long version = 20) => new("io.alpha.app", version, "2.0", [Cert]);
    static string Decisions(string status = "decided", string? option = null, string? record = null) => JsonSerializer.Serialize(new
    {
        decisions = new[] { new { id = "DEC-0040", status, record = record ?? "docs/governance/responses/DEC-0040.md",
            decision = "Alternativa A — " + (option ?? Trust.ApprovalOption) + " (escolhida pelo proprietário pelo portal; Issue https://github.com/acme/catalog/issues/1)." } }
    });

    [Fact] public void Exact_current_canonical_approval_is_required()
    {
        Assert.Equal(Trust, InstallationPolicy.Approved(Trust, Datum<string>.From(Decisions(), "decisions")));
        Assert.Null(InstallationPolicy.Approved(Trust, Datum<string>.From(Decisions(), "decisions").AsStale()));
        Assert.Null(InstallationPolicy.Approved(Trust, Datum<string>.Missing("decisions")));
    }
    [Theory] [InlineData("pending")] [InlineData("withdrawn")] [InlineData("bad")]
    public void Pending_or_revoked_is_not_permission(string state) => Assert.Null(InstallationPolicy.Approved(Trust, Datum<string>.From(Decisions(state), "decisions")));
    [Theory] [InlineData("")] [InlineData("{}")] [InlineData("[]")] [InlineData("{broken")]
    public void Malformed_approval_denies(string json) => Assert.Null(InstallationPolicy.Approved(Trust, Datum<string>.From(json, "decisions")));
    [Fact] public void Approval_cannot_be_reused_for_other_package_certificate_channel_or_option()
    {
        var source = Datum<string>.From(Decisions(), "decisions");
        Assert.Null(InstallationPolicy.Approved(Trust with { PackageId = "io.other.app" }, source));
        Assert.Null(InstallationPolicy.Approved(Trust with { CertificateSha256 = new('B',64) }, source));
        Assert.Null(InstallationPolicy.Approved(Trust with { Repository = "other/catalog" }, source));
        Assert.Null(InstallationPolicy.Approved(Trust with { TagPrefix = "beta-v" }, source));
        Assert.Null(InstallationPolicy.Approved(Trust, Datum<string>.From(Decisions(option: "B — Adiar"), "decisions")));
        Assert.Null(InstallationPolicy.Approved(Trust, Datum<string>.From(Decisions(record: "unrelated.md"), "decisions")));
    }
    [Fact] public void Ambiguous_decision_denies()
    {
        using var doc = JsonDocument.Parse(Decisions());
        var one = doc.RootElement.GetProperty("decisions")[0].GetRawText();
        Assert.Null(InstallationPolicy.Approved(Trust, Datum<string>.From("{\"decisions\":["+one+","+one+"]}","decisions")));
    }
    [Fact] public void First_install_and_monotonic_update_allowed()
    {
        Assert.True(InstallationPolicy.Evaluate(Trust, Choice, Package(), null, true).Allowed);
        Assert.True(InstallationPolicy.Evaluate(Trust, Choice, Package(), Package(19), true).Allowed);
    }
    [Fact] public void Missing_trust_and_consent_deny()
    {
        Assert.False(InstallationPolicy.Evaluate(null, Choice, Package(), null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice, Package(), null, false).Allowed);
    }
    [Fact] public void Wrong_product_repo_prefix_or_hash_denies()
    {
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice with { ProductId="beta" }, Package(), null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice with { Channel=new(new("bad","repo"),"https://github.com/bad/repo/releases","alpha-v") }, Package(), null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice with { Channel=Choice.Channel with { TagPrefix="v" } }, Package(), null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice with { Asset=Choice.Asset with { Sha256=null } }, Package(), null, true).Allowed);
    }
    [Fact] public void Wrong_or_missing_apk_metadata_denies()
    {
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice, null, null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice, Package() with { PackageId="io.other.app" }, null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice, Package(0), null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice, Package() with { CurrentSigners=[] }, null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice, Package() with { CurrentSigners=[new('B',64)] }, null, true).Allowed);
        Assert.False(InstallationPolicy.Evaluate(Trust, Choice, Package() with { CurrentSigners=[Cert,new('B',64)] }, null, true).Allowed);
    }
    [Theory] [InlineData(20)] [InlineData(21)]
    public void Same_version_or_downgrade_is_denied(long installed) => Assert.False(InstallationPolicy.Evaluate(Trust,Choice,Package(),Package(installed),true).Allowed);
    [Fact] public void Incompatible_installed_signer_is_never_removed_or_ignored() => Assert.False(InstallationPolicy.Evaluate(Trust, Choice, Package(), Package(19) with { CurrentSigners=[new('B',64)] }, true).Allowed);
    [Fact] public void Colon_separated_fingerprint_is_normalized() => Assert.True(InstallationPolicy.TrustedPackage(Trust,Package() with { CurrentSigners=[string.Join(":",Enumerable.Repeat("aa",32))] }));
    [Fact] public async Task Verified_copy_equals_exact_original_bytes()
    {
        using var input = new MemoryStream(Bytes); using var output = new MemoryStream();
        await InstallationPolicy.CopyVerifiedAsync(input,output,Bytes.Length,Hash,TestContext.Current.CancellationToken);
        Assert.Equal(Bytes,output.ToArray());
    }
    [Theory] [InlineData(-1)] [InlineData(1)]
    public async Task Changed_length_rejects(int difference)
    {
        using var input=new MemoryStream(Bytes);using var output=new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(()=>InstallationPolicy.CopyVerifiedAsync(input,output,Bytes.Length+difference,Hash,TestContext.Current.CancellationToken));
    }
    [Fact] public async Task Changed_bytes_reject_even_when_prior_download_was_verified()
    {
        var altered=Bytes.ToArray();altered[80000]^=1;
        using var input=new MemoryStream(altered);using var output=new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(()=>InstallationPolicy.CopyVerifiedAsync(input,output,Bytes.Length,Hash,TestContext.Current.CancellationToken));
    }
    [Fact] public async Task Cancellation_before_copy_writes_nothing()
    {
        using var ct=new CancellationTokenSource();ct.Cancel();using var input=new MemoryStream(Bytes);using var output=new MemoryStream();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>InstallationPolicy.CopyVerifiedAsync(input,output,Bytes.Length,Hash,ct.Token));Assert.Equal(0,output.Length);
    }
    [Fact] public void Old_duplicate_or_foreign_session_result_does_not_match_current_operation()
    {
        Assert.True(InstallationResultPolicy.Matches(1,"one",1,"one"));
        Assert.False(InstallationResultPolicy.Matches(2,"two",1,"one"));
        Assert.False(InstallationResultPolicy.Matches(1,"one",1,"two"));
        Assert.False(InstallationResultPolicy.Matches(-1,"one",-1,"one"));
        Assert.False(InstallationResultPolicy.Matches(1,"",1,""));
    }
    [Fact] public void Callback_success_is_not_proof_without_installed_package_evidence()
    {
        Assert.True(InstallationResultPolicy.Confirmed(Trust.PackageId,20,Cert,Package()));
        Assert.False(InstallationResultPolicy.Confirmed(Trust.PackageId,20,Cert,null));
        Assert.False(InstallationResultPolicy.Confirmed(Trust.PackageId,20,Cert,Package(19)));
        Assert.False(InstallationResultPolicy.Confirmed(Trust.PackageId,20,Cert,Package() with { PackageId="io.other.app" }));
        Assert.False(InstallationResultPolicy.Confirmed(Trust.PackageId,20,Cert,Package() with { CurrentSigners=[new('B',64)] }));
    }
}
