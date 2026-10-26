using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class AppUpdateHistoryTests
{
    [Fact]
    public void CatalogSortsAllNativeReleasesAndRejectsForeignMissingAndDraftAssets()
    {
        var json = """
        [
          {"tag_name":"lunet-v9.0.0","assets":[{"name":"Urbe-CSharp-9.0.0-Android.apk","browser_download_url":"https://github.com/AbnerCruz/Ecosystem/releases/download/lunet-v9.0.0/Urbe-CSharp-9.0.0-Android.apk"}]},
          {"tag_name":"urbe-csharp-v2.0.0-beta.9","body":"antiga","assets":[{"name":"Urbe-CSharp-2.0.0-beta.9-Android.apk","browser_download_url":"https://github.com/AbnerCruz/Ecosystem/releases/download/urbe-csharp-v2.0.0-beta.9/Urbe-CSharp-2.0.0-beta.9-Android.apk"}]},
          {"tag_name":"urbe-csharp-v2.0.0-beta.10","body":"nova","assets":[{"name":"Urbe-CSharp-2.0.0-beta.10-Android.apk","browser_download_url":"https://github.com/AbnerCruz/Ecosystem/releases/download/urbe-csharp-v2.0.0-beta.10/Urbe-CSharp-2.0.0-beta.10-Android.apk"}]},
          {"tag_name":"urbe-csharp-v2.0.0-beta.11","draft":true,"assets":[{"name":"Urbe-CSharp-2.0.0-beta.11-Android.apk","browser_download_url":"https://github.com/AbnerCruz/Ecosystem/releases/download/urbe-csharp-v2.0.0-beta.11/Urbe-CSharp-2.0.0-beta.11-Android.apk"}]},
          {"tag_name":"urbe-csharp-v2.0.0-beta.12","assets":[{"name":"Urbe-CSharp-2.0.0-beta.12-Android.apk","browser_download_url":"https://malicious.example/Urbe-CSharp-2.0.0-beta.12-Android.apk"}]}
        ]
        """;
        var all = AppUpdate.List(json, AppUpdate.AndroidAsset);
        Assert.Equal(["2.0.0-beta.10", "2.0.0-beta.9"], all.Select(x => x.Version).ToArray());
        Assert.Equal("nova", all[0].Notes);
        Assert.Equal("2.0.0-beta.10", AppUpdate.Pick(json, AppUpdate.AndroidAsset)!.Version);
        Assert.Empty(AppUpdate.List("invalid json", AppUpdate.AndroidAsset));
        Assert.Empty(AppUpdate.List("{}", AppUpdate.AndroidAsset));
        Assert.Empty(AppUpdate.List("[]", AppUpdate.WindowsAsset));
    }

    [Fact]
    public void CatalogKeepsPublishedTagAndAssetPathsExact()
    {
        var json = """[{"tag_name":"urbe-csharp-v2.0.0-beta.31","assets":[{"name":"Urbe-CSharp-2.0.0-beta.31-Android.apk","browser_download_url":"https://github.com/AbnerCruz/Ecosystem/releases/download/urbe-csharp-v2.0.0-beta.31/Urbe-CSharp-2.0.0-beta.31-Android.apk"}]}]""";
        var release = Assert.Single(AppUpdate.List(json, AppUpdate.AndroidAsset));
        Assert.EndsWith("Urbe-CSharp-2.0.0-beta.31-Android.apk", release.Url);
    }
}
