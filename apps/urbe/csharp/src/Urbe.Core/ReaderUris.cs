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
        if (id is null || id.Length != 11 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) return null;
        string time = query.GetValueOrDefault("t") ?? query.GetValueOrDefault("start") ?? "0";
        int second = 0;
        if (int.TryParse(time.TrimEnd('s'), out int parsed) && parsed >= 0) second = parsed;
        return (id, second);
    }
}
