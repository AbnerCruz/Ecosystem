using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-24/UC-25: the update check picks the newest release of the native client with this platform's file.</summary>
public sealed class AppUpdateTests
{
    [Theory]
    [InlineData("2.0.0-beta.10", "2.0.0-beta.9", true)]
    [InlineData("2.0.0-beta.9", "2.0.0-beta.10", false)]
    [InlineData("2.0.0", "2.0.0-beta.18", true)]
    [InlineData("2.0.0-beta.18", "2.0.0", false)]
    [InlineData("2.0.1", "2.0.0", true)]
    [InlineData("2.0.0-beta.18", "2.0.0-beta.18", false)]
    [InlineData("v2.1.0", "2.0.9", true)]
    [InlineData("lixo", "2.0.0", false)]
    [InlineData("2.0.0", null, false)]
    public void NewerComparesLikeThe184(string? a, string? b, bool newer) => Assert.Equal(newer, AppUpdate.Newer(a, b));

    private static string Release(string tag, string asset, bool draft = false, string host = "https://github.com/AbnerCruz/Ecosystem/releases/download/") =>
        "{\"tag_name\":\"" + tag + "\",\"draft\":" + (draft ? "true" : "false") + ",\"body\":\"Notas de " + tag + "\",\"assets\":[{\"name\":\"" + asset +
        "\",\"browser_download_url\":\"" + host + tag + "/" + asset + "\"}]}";

    [Fact]
    public void PickTakesTheNewestNativeReleaseWithThePlatformsFile()
    {
        var json = "[" + string.Join(",",
            Release("urbe-csharp-v2.0.0-beta.9", "Urbe-CSharp-2.0.0-beta.9-Android.apk"),
            Release("urbe-csharp-v2.0.0-beta.18", "Urbe-CSharp-2.0.0-beta.18-Windows-x64.zip"),   // only Windows
            Release("urbe-csharp-v2.0.0-beta.17", "Urbe-CSharp-2.0.0-beta.17-Android.apk"),
            Release("urbe-csharp-v2.0.0-beta.19", "Urbe-CSharp-2.0.0-beta.19-Android.apk", draft: true),
            Release("urbe-csharp-v2.0.0-beta.20", "Urbe-CSharp-2.0.0-beta.20-Android.apk", host: "https://exemplo.com/"),
            Release("urbe-v1.9.0", "Urbe-CSharp-1.9.0-Android.apk"),   // the 1.8.4's channel
            Release("lunet-v9.0.0", "Urbe-CSharp-9.0.0-Android.apk")) + "]";
        var android = AppUpdate.Pick(json, AppUpdate.AndroidAsset);
        Assert.Equal("2.0.0-beta.17", android!.Version);
        Assert.Equal("https://github.com/AbnerCruz/Ecosystem/releases/download/urbe-csharp-v2.0.0-beta.17/Urbe-CSharp-2.0.0-beta.17-Android.apk", android.Url);
        Assert.Equal("Notas de urbe-csharp-v2.0.0-beta.17", android.Notes);
        Assert.Equal("2.0.0-beta.18", AppUpdate.Pick(json, AppUpdate.WindowsAsset)!.Version);
        Assert.Null(AppUpdate.Pick("[]", AppUpdate.AndroidAsset));
        Assert.Null(AppUpdate.Pick("{\"message\":\"rate limit\"}", AppUpdate.AndroidAsset));
        Assert.Null(AppUpdate.Pick("não é json", AppUpdate.AndroidAsset));
    }
}
