using Hub.Core;

namespace Hub.Tests;

public class DistributionReaderTests
{
    static readonly RepositoryRef Ecosystem = new("acme", "ecosystem");
    static Datum<string> D(string s) => Datum<string>.From(s, "ecosystem.json");
    static ProductSummary Product(string? repository = "https://github.com/acme/origin", string? url = "https://github.com/acme/ecosystem/releases") =>
        new("alpha", D("Alpha"), D("product"), D("active"), D("1.0.0"),
            repository is null ? Datum<string>.Missing("source.repository") : D(repository),
            url is null ? Datum<string>.Missing("publicUrl") : D(url));

    static string Profile(string location = "source.repository", string kind = "github-release", string status = "current") => $$"""
        {"status":"{{status}}","entries":[{"component":"alpha","channels":[
         {"kind":"{{kind}}","role":"primary","locationFrom":"{{location}}"}]}]}
        """;
    static Datum<ReleaseChannel> Read(string text, ProductSummary? p = null) => DistributionReader.ReleaseChannelFor(
        p ?? Product(), Datum<string>.From(text, DistributionReader.ProfilePath), Ecosystem);

    [Fact]
    public void ResolvesOriginAndMonorepoChannelsFromTheDeclaredLocation()
    {
        var origin = Read(Profile());
        Assert.Equal("acme/origin", origin.Value!.Repository.FullName);
        Assert.Null(origin.Value.TagPrefix);
        var own = Read(Profile("ecosystem.repository"));
        Assert.Equal("acme/ecosystem", own.Value!.Repository.FullName);
        Assert.Equal("alpha-v", own.Value.TagPrefix);
        Assert.Equal("https://github.com/acme/ecosystem/releases", own.Value.ReleasesUrl);
        Assert.Contains(DistributionReader.ProfilePath, own.Source);

        var publicUrl = Read(Profile("publicUrl"));
        Assert.Equal("acme/ecosystem", publicUrl.Value!.Repository.FullName);
        Assert.Equal("alpha-v", publicUrl.Value.TagPrefix);
    }

    [Theory]
    [InlineData("publicUrl", "https://acme.invalid/web")]
    [InlineData("publicUrl", "http://github.com/acme/ecosystem/releases")]
    [InlineData("publicUrl", null)]
    [InlineData("unsupported", "https://github.com/acme/ecosystem/releases")]
    public void InvalidLocationCannotFallBackToTheOrigin(string location, string? url)
    {
        var d = Read(Profile(location), Product(url: url));
        Assert.Equal(Availability.NotAvailable, d.Availability);
        Assert.Contains("localização", d.Note);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("{\"status\":\"current\",\"entries\":[]}")]
    [InlineData("{\"status\":\"current\",\"entries\":[{\"component\":\"alpha\"}]}")]
    public void IncompleteOrMalformedProfilesStayUnavailable(string json) =>
        Assert.Equal(Availability.NotAvailable, Read(json).Availability);

    [Fact]
    public void TargetAndWebChannelsAreNotTreatedAsOperationalReleases()
    {
        Assert.Equal(Availability.NotAvailable, Read(Profile(status: "target")).Availability);
        Assert.Equal(Availability.NotAvailable, Read(Profile(kind: "github-pages")).Availability);
    }

    [Fact]
    public void AmbiguousPrimaryChannelsAreNotChosenArbitrarily()
    {
        var d = Read("""
        {"status":"current","entries":[{"component":"alpha","channels":[
          {"kind":"github-release","role":"primary","locationFrom":"source.repository"},
          {"kind":"github-release","role":"primary","locationFrom":"publicUrl"}]}]}
        """);
        Assert.Equal(Availability.NotAvailable, d.Availability);
        Assert.Contains("ambíguo", d.Note);
    }

    [Fact]
    public void MissingProfilePreservesItsFailureReason()
    {
        var d = DistributionReader.ReleaseChannelFor(Product(), Datum<string>.Missing("profile", "HTTP 403"), Ecosystem);
        Assert.Equal("HTTP 403", d.Note);
        Assert.Equal(Availability.NotAvailable, d.Availability);
    }
}
