using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>DEC-0045 (UC-43): Personalização → Tags chooses the colour of each #Tag:, kept in Personalização/tags.json.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public void TagStyles_FileRoundTripsAndIgnoresWhatIsNotATagOrAColour()
    {
        var text = TagStyles.Text(new Dictionary<string, string> { ["Todo"] = "#7FD0DC", ["Projeto"] = "#c79bff", ["mau nome"] = "#ffffff", ["Ok"] = "verde" });
        Assert.Equal("{\n  \"$schema\": \"urbe-tags-1\",\n  \"tags\": {\n    \"Projeto\": \"#c79bff\",\n    \"Todo\": \"#7fd0dc\"\n  }\n}\n", text.Replace("\r\n", "\n"));
        var back = TagStyles.Parse(text);
        Assert.Equal("#7fd0dc", back["todo"]);
        Assert.Equal(2, back.Count);
        Assert.Empty(TagStyles.Parse("{ não é json"));
    }

    [Fact]
    public async Task TagStyles_ChosenInPersonalizationAndWrittenToTheVault() =>
        Assert.True(await Session.Dispatch(TagStylesScenario, CancellationToken.None));

    private static async Task<bool> TagStylesScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-tags-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Casa.md"), "#Todo: revisar\n");
        async Task Settled() { for (int i = 0; i < 30; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settled();
            Assert.Equal(UrbeTheme.Warn, UrbeTheme.TagColor("Todo"));

            view.Customize.Open("tags");
            Dispatcher.UIThread.RunJobs();
            var labels = view.Customize.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            foreach (var t in TagStyles.Defaults) Assert.Contains("#" + t, labels);
            Snapshot(window, "native-customize-tags.png");

            // a colour for #Todo: written to Personalização/tags.json and used at once
            var dot = view.Customize.GetLogicalDescendants().OfType<Button>()
                .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "#Todo #7fd0dc");
            dot.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settled();
            var file = Path.Combine(vault, "Personalização", "tags.json");
            Assert.Equal("#7fd0dc", TagStyles.Parse(File.ReadAllText(file))["Todo"]);
            Assert.Equal("#7fd0dc", UrbeTheme.TagColor("todo"));
            Assert.False(File.Exists(Path.Combine(vault, "Personalização", "tema.json")) && File.ReadAllText(Path.Combine(vault, "Personalização", "tema.json")).Contains("tags", StringComparison.Ordinal));

            // Padrão: back to the app's colour
            ClickButtonWithText(view.Customize, "Padrão");
            await Settled();
            Assert.Empty(TagStyles.Parse(File.ReadAllText(file)));
            Assert.Equal(UrbeTheme.Warn, UrbeTheme.TagColor("Todo"));
            window.Close();
            return true;
        }
        finally
        {
            UrbeTheme.TagOverrides = new Dictionary<string, string>();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
