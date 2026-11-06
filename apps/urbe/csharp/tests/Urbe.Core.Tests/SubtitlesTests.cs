using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-210: subtitles from the reviewed transcription note (the note is the source).</summary>
public sealed class SubtitlesTests
{
    [Fact]
    public void FromTranscript_TimesFollowTheParts_LongPartsSplitIntoTwoLineCues()
    {
        var parts = new[] { (TimeSpan.Zero, (string?)"Bom dia, turma."), (TimeSpan.FromSeconds(4), "Hoje vamos estudar **derivadas** e ver como elas descrevem a taxa de variação de uma função em cada ponto do domínio."), (TimeSpan.FromSeconds(20), null), (TimeSpan.FromSeconds(25), "Fim.") };
        var note = LongAudio.Note("Aulas/aula.m4a", parts);
        Assert.True(Subtitles.HasTimedParts(note));
        var cues = Subtitles.FromTranscript(note, TimeSpan.FromSeconds(27));
        Assert.Equal("Bom dia, turma.", cues[0].Text);
        Assert.Equal(TimeSpan.Zero, cues[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(4), cues[0].End);
        var long1 = cues.Where(c => c.Start >= TimeSpan.FromSeconds(4) && c.Start < TimeSpan.FromSeconds(20)).ToList();
        Assert.True(long1.Count >= 2);
        Assert.All(long1, c => Assert.All(c.Text.Split('\n'), l => Assert.True(l.Length <= Subtitles.LineChars, l)));
        Assert.All(long1, c => Assert.True(c.Text.Split('\n').Length <= 2));
        Assert.All(cues, c => Assert.True((c.End - c.Start).TotalSeconds is >= 1 and <= Subtitles.MaxSeconds));
        Assert.DoesNotContain(cues, c => c.Text.Contains("**") || c.Text.Contains("não reconhecido"));
        Assert.Equal("Fim.", cues[^1].Text);
        Assert.Equal(TimeSpan.FromSeconds(25), cues[^1].Start);
        var srt = Subtitles.ToSrt(cues);
        Assert.StartsWith("1\r\n00:00:00,000 --> 00:00:04,000\r\nBom dia, turma.\r\n\r\n2\r\n00:00:04,000 --> ", srt);
        var vtt = Subtitles.ToVtt(cues);
        Assert.StartsWith("WEBVTT\n\n00:00:00.000 --> 00:00:04.000\nBom dia, turma.\n", vtt);
        Assert.False(Subtitles.HasTimedParts("# nota comum\n\n**[negrito]** sem horário"));
    }
}
