using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-161: a new publication takes another one's look and audience, never its notes.</summary>
    [Fact]
    public async Task Publication_InTheStyleOfAnother() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubstyle-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Publicações"));
        File.WriteAllText(Path.Combine(vault, "Nota.md"), "# Nota\n\nTexto.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sty.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Wait(Func<bool> ready) { for (int i = 0; i < 300 && !ready(); i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var model = Urbe.Core.PublicationProject.Create("Guia", "Nota.md") with { Audience = "Crianças", Design = new() { Accent = "#225588" } };
            File.WriteAllText(Path.Combine(vault, "Publicações", "Guia.publication.json"), Urbe.Core.PublicationProjectCodec.Serialize(model));
            await view.OpenVaultAsync();
            await Settle(window, view);
            var run = view.CreatePublicationInStyleAsync();
            await Wait(() => view.Dialogs.IsOpen);
            ClickButtonWithText(view.Dialogs, "Guia");
            await Wait(() => view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Any());
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Segundo";
            ClickButtonWithText(view.Dialogs, "Criar");
            await run;
            await Settle(window, view);
            var made = Urbe.Core.PublicationProjectCodec.Parse(File.ReadAllText(Path.Combine(vault, "Publicações", "Segundo.publication.json")));
            Assert.Equal("Crianças", made.Audience);
            Assert.Equal("#225588", made.Design.Accent);
            Assert.Empty(made.Sources);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
