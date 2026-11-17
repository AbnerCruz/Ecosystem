namespace Urbe.Core;

public static class ReaderUris
{
    public static Uri? Web(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") ||
            !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(uri.Host)) return null;
        return uri;
    }

    public static (string Id, int Second)? YouTube(string value)
    {
        if (Web(value) is not { } uri) return null;
        string host = uri.IdnHost.ToLowerInvariant();
        string? id = null;
        var query = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
            .GroupBy(p => p[0], StringComparer.Ordinal).ToDictionary(g => g.Key, g => Uri.UnescapeDataString(g.First()[1]), StringComparer.Ordinal);
        if (host is "youtu.be" or "www.youtu.be") id = uri.AbsolutePath.Trim('/');
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "www.youtube-nocookie.com")
            id = query.GetValueOrDefault("v") ?? (uri.AbsolutePath.Split('/') is { Length: >= 3 } paths && paths[1] is "embed" or "shorts" ? paths[2] : null);
        // "videoseries" is the playlist player's path, not a video
        if (id is null || id == "videoseries" || id.Length != 11 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) return null;
        return (id, Seconds(query.GetValueOrDefault("t") ?? query.GetValueOrDefault("start") ?? "0"));
    }

    /// <summary>CST-236: a share link's time: "150", "150s", "2m30s", "1h2m3s" (a share link used to open at 0).</summary>
    public static int Seconds(string time)
    {
        if (int.TryParse(time.TrimEnd('s'), out int plain)) return Math.Max(0, plain);
        var m = System.Text.RegularExpressions.Regex.Match(time, @"^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$");
        if (!m.Success || time.Length == 0) return 0;
        static int Part(System.Text.RegularExpressions.Group g) => g.Success ? int.Parse(g.Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        return Part(m.Groups[1]) * 3600 + Part(m.Groups[2]) * 60 + Part(m.Groups[3]);
    }

    /// <summary>CST-236: the public playlist of a YouTube link (list=…), or null.</summary>
    public static string? YouTubePlaylist(string value)
    {
        if (Web(value) is not { } uri || uri.IdnHost.ToLowerInvariant() is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "www.youtube-nocookie.com")) return null;
        var list = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).FirstOrDefault(p => p.Length == 2 && p[0] == "list")?[1];
        return list is { Length: >= 2 and <= 64 } && list.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? list : null;
    }

    /// <summary>
    /// CST-236: the official embedded player for a YouTube video or playlist link (the video at its second, inside its
    /// playlist when there is one), or null for anything else.
    /// </summary>
    public static Uri? YouTubeEmbed(string value)
    {
        var video = YouTube(value);
        var list = YouTubePlaylist(value);
        if (video is { } v) return new Uri("https://www.youtube.com/embed/" + v.Id + "?start=" + v.Second + (list is null ? "" : "&list=" + list));
        return list is null ? null : new Uri("https://www.youtube.com/embed/videoseries?list=" + list);
    }

    /// <summary>Whether an address is the official embedded player (a video or a playlist), which needs the app's Referer.</summary>
    public static bool IsYouTubeEmbed(string value) =>
        Web(value) is { } uri && uri.IdnHost.ToLowerInvariant() is "www.youtube.com" or "youtube.com" or "www.youtube-nocookie.com" &&
        uri.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal) && (YouTube(value) is not null || YouTubePlaylist(value) is not null);
}
