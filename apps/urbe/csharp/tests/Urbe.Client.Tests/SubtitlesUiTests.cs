using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-210: a transcription note exports its reviewed text as subtitles.</summary>
    [Fact]
    public async Task Subtitles_ExportFromTheReviewedTranscription() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        await view.LoadAsync(TutorialNotes.Load());
        var note = LongAudio.Note("Aula.m4a", [(TimeSpan.Zero, "Olá, turma revisada."), (TimeSpan.FromSeconds(3), "Segundo trecho.")]);
        (string Name, byte[] Bytes)? saved = null;
        view.SavePageFile = (n, b) => { saved = (n, b); return Task.FromResult<bool?>(true); };
        var exporting = view.ExportSubtitlesAsync("Aula — transcrição.md", note);
        Dispatcher.UIThread.RunJobs();
        ClickButtonWithText(view.Dialogs, "SRT");
        await exporting;
        Assert.Equal("Aula.srt", saved!.Value.Name);
        var srt = System.Text.Encoding.UTF8.GetString(saved.Value.Bytes);
        Assert.Contains("Olá, turma revisada.", srt);
        Assert.Contains("00:00:03,000 --> ", srt);
        window.Close();
        return true;
    }, CancellationToken.None));
}
