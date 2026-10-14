using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>openRegionEditDialog → confirmRegion: name, description and colour; a new name renames the folder in the vault.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task RegionEdit_RenamesTheFolderInTheVaultAndKeepsEverythingInside() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                var bairro = city.RegionByPath("Bairro")!;
                var um = city.Buildings.Single(b => b.Path == "Bairro/Um.md");

                // the dialog: name, description, colour, "Salvar alterações"
                var edit = view.EditRegionAsync(bairro);
                Dispatcher.UIThread.RunJobs();
                var boxes = view.Dialogs.GetVisualDescendants().OfType<TextBox>().ToList();
                Assert.Equal(2, boxes.Count);
                Assert.Equal("Bairro", boxes[0].Text);
                Snapshot(window, "native-region-edit.png");
                boxes[0].Text = "Centro";
                boxes[1].Text = "Onde tudo começa.";
                var green = view.Dialogs.GetVisualDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Cor #7ee3a0");
                green.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                ClickButtonWithText(view.Dialogs, "Salvar alterações");
                await edit;

                // the folder renamed on disk, the note inside it, the house and its document following
                Assert.False(Directory.Exists(Path.Combine(vault, "Bairro")));
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Centro", "Um.md")));
                Assert.Equal("Centro", bairro.Name);
                Assert.Equal("Onde tudo começa.", bairro.Description);
                Assert.Equal("#7ee3a0", bairro.Color);
                Assert.Equal("Centro/Um.md", um.Path);
                Assert.Same(bairro, city.RegionByPath("Centro"));
                await UntilMapSaved(view);
                var mapa = File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json"));
                Assert.Contains("\"Centro/Um.md\"", mapa);
                Assert.DoesNotContain("\"Bairro/Um.md\"", mapa);

                // only the colour: nothing on disk moves
                await view.EditRegionAsync(bairro, ("Centro", "Onde tudo começa.", "#ffd567"));
                Assert.Equal("#ffd567", bairro.Color);
                Assert.True(File.Exists(Path.Combine(vault, "Centro", "Um.md")));

                // a name another folder already has: refused, nothing changes
                var outro = city.CreateFolder("Outro2", null, 1)!;
                var refused = view.EditRegionAsync(bairro, (city.RegionPaths()[outro.Id], "", "#ffd567"));
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.Dialogs.IsOpen);
                ClickButtonWithText(view.Dialogs, "Ok");
                await refused;
                Assert.Equal("Centro", bairro.Name);
                Assert.True(Directory.Exists(Path.Combine(vault, "Centro")));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
