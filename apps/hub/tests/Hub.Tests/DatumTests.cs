using Hub.Core;

namespace Hub.Tests;

public class DatumTests
{
    [Fact]
    public void FromCarriesValueAndSource()
    {
        var d = Datum<string>.From("0.0.1", "componente/VERSION");
        Assert.Equal(Availability.Derived, d.Availability);
        Assert.Equal("0.0.1", d.Value);
        Assert.Equal("componente/VERSION", d.Source);
    }

    [Fact]
    public void MissingIsNotAvailableAndHasNoValue()
    {
        var d = Datum<string>.Missing("ecosystem.json");
        Assert.Equal(Availability.NotAvailable, d.Availability);
        Assert.Null(d.Value);
    }

    [Fact]
    public void AsStaleMarksOnlyWhatWasDerived()
    {
        Assert.Equal(Availability.Stale, Datum<int>.From(1, "s").AsStale().Availability);
        Assert.Equal(Availability.NotAvailable, Datum<int>.Missing("s").AsStale().Availability);
    }
}
