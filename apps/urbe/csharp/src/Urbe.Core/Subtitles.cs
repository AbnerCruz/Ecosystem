using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>A subtitle: when it shows, when it goes, and its (at most two) lines.</summary>
public sealed record SubtitleCue(TimeSpan Start, TimeSpan End, string Text);

/// <summary>
/// CST-210: subtitles (SRT, WebVTT) from a transcription note — the note the person reviewed is the source, so a
/// corrected word is corrected in the subtitles. Each <c>**[mm:ss]**</c> part runs until the next one; a long part is
/// split into cues of at most two lines of 42 characters, its time shared in proportion to the text, no cue shorter
/// than a second or longer than seven.
/// </summary>
public static partial class Subtitles
{
    public const int LineChars = 42, MaxSeconds = 7;

    /// <summary>Whether a note has timed parts to make subtitles from.</summary>
    public static bool HasTimedParts(string markdown) => PartRe().IsMatch(markdown);

    public static IReadOnlyList<SubtitleCue> FromTranscript(string markdown, TimeSpan? total = null)
    {
        var parts = new List<(TimeSpan At, string Text)>();
        foreach (Match m in PartRe().Matches(markdown.Replace("\r", "")))
        {
            var text = Regex.Replace(m.Groups["text"].Value, @"\s+", " ").Trim();
            if (text.Length == 0 || text == "(trecho não reconhecido)") { parts.Add((Time(m), "")); continue; }
            parts.Add((Time(m), Strip(text)));
        }
        var cues = new List<SubtitleCue>();
        for (int i = 0; i < parts.Count; i++)
        {
            var (start, text) = parts[i];
            if (text.Length == 0) continue;
            var end = i + 1 < parts.Count ? parts[i + 1].At : total ?? start + TimeSpan.FromSeconds(Math.Max(2, text.Length / 15.0));
            if (end <= start) end = start + TimeSpan.FromSeconds(2);
            cues.AddRange(Split(start, end, text));
        }
        return cues;
    }

    private static TimeSpan Time(Match m)
    {
        int h = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture) : 0;
        return new TimeSpan(h, int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Markdown marks out (bold, italics, links), the words kept.</summary>
    private static string Strip(string text) =>
        Regex.Replace(Regex.Replace(Regex.Replace(text, @"\[([^\]]+)\]\([^)]*\)", "$1"), @"[*_`~]+", ""), @"\[\[([^\]|]+\|)?([^\]]+)\]\]", "$2").Trim();

    private static IEnumerable<SubtitleCue> Split(TimeSpan start, TimeSpan end, string text)
    {
        // words into cues of up to two lines of LineChars
        var cues = new List<List<string>>();
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > LineChars)
            {
                lines.Add(line.ToString());
                line.Clear();
                if (lines.Count == 2) { cues.Add(lines); lines = []; }
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }
        if (line.Length > 0) lines.Add(line.ToString());
        if (lines.Count > 0) cues.Add(lines);
        var total = (end - start).TotalSeconds;
        var chars = cues.Sum(c => c.Sum(l => l.Length));
        var at = start.TotalSeconds;
        foreach (var cue in cues)
        {
            var share = total * cue.Sum(l => l.Length) / Math.Max(1, chars);
            var length = Math.Clamp(share, 1, MaxSeconds);
            var cueEnd = Math.Min(end.TotalSeconds, at + length);
            if (cueEnd <= at) cueEnd = at + 1;
            yield return new SubtitleCue(TimeSpan.FromSeconds(at), TimeSpan.FromSeconds(cueEnd), string.Join("\n", cue));
            at += share;
        }
    }

    public static string ToSrt(IReadOnlyList<SubtitleCue> cues)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < cues.Count; i++)
            sb.Append(i + 1).Append("\r\n").Append(Stamp(cues[i].Start, ',')).Append(" --> ").Append(Stamp(cues[i].End, ',')).Append("\r\n")
              .Append(cues[i].Text.Replace("\n", "\r\n")).Append("\r\n\r\n");
        return sb.ToString();
    }

    public static string ToVtt(IReadOnlyList<SubtitleCue> cues)
    {
        var sb = new StringBuilder("WEBVTT\n\n");
        foreach (var c in cues)
            sb.Append(Stamp(c.Start, '.')).Append(" --> ").Append(Stamp(c.End, '.')).Append('\n').Append(c.Text.Replace("-->", "→")).Append("\n\n");
        return sb.ToString();
    }

    private static string Stamp(TimeSpan t, char separator) =>
        ((int)t.TotalHours).ToString("00", CultureInfo.InvariantCulture) + ":" + t.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
        t.Seconds.ToString("00", CultureInfo.InvariantCulture) + separator + t.Milliseconds.ToString("000", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\*\*\[(?:(?<h>\d+):)?(?<m>\d{1,2}):(?<s>\d{2})\]\*\*[ \t]*(?<text>.*)$", RegexOptions.Multiline)]
    private static partial Regex PartRe();
}
