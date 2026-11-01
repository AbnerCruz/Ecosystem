using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Publications_FailedCompositionSaveSurvivesCloseAndCanBeRetried() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        const string path = "Livro.publication.json";
        var project = PublicationProject.Create("Antes");
        var original = PublicationProjectCodec.Serialize(project);
        using var vault = await OpenFaultyVaultAsync((path, original));
        var view = vault.View;
        await view.OpenPublicationAsync(path);
        vault.Storage.Fail("save");
        view.PublicationsStudio.UpdateProject(project with { Title = "Depois" });
        await Settle(vault.Window, view);

        Assert.Equal(original, vault.Read(path));
        Assert.True(view.PublicationsStudio.HasUnsavedChanges);
        Assert.NotNull(view.PublicationsStudio.SaveError);
        view.PublicationsStudio.Close();
        await view.OpenPublicationAsync(path);
        Assert.Equal("Depois", view.PublicationsStudio.Project!.Title);
        Assert.True(view.PublicationsStudio.HasUnsavedChanges);
        Assert.NotNull(view.PublicationsStudio.SaveError);

        Assert.True(await view.SavePublicationAsync());
        Assert.Equal("Depois", PublicationProjectCodec.Parse(vault.Read(path)).Title);
        Assert.False(view.PublicationsStudio.HasUnsavedChanges);
        Assert.Null(view.PublicationsStudio.SaveError);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Publications_ExternalCompositionChangeCannotBeSilentlyOverwrittenOrExported() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        const string path = "Livro.publication.json";
        var project = PublicationProject.Create("Antes");
        using var vault = await OpenFaultyVaultAsync((path, PublicationProjectCodec.Serialize(project)));
        var view = vault.View;
        await view.OpenPublicationAsync(path);
        // An agent changed the same real file through the canonical write path.
        var app = (Urbe.Client.IAssistantApp)view;
        var external = PublicationProjectCodec.Serialize(project with { Title = "Outro autor" });
        await app.SaveNoteAsync(view.Documents!.Get(path)!, external);
        view.PublicationsStudio.UpdateProject(project with { Title = "Meu rascunho" });
        await Settle(vault.Window, view);
        Assert.False(await view.SavePublicationAsync());
        Assert.Equal(external, vault.Read(path));
        Assert.Equal("Meu rascunho", view.PublicationsStudio.Project!.Title);
        Assert.NotNull(view.PublicationsStudio.SaveError);

        bool exported = false;
        view.SavePageFile = (_, _) => { exported = true; return Task.FromResult<bool?>(true); };
        var button = view.PublicationsStudio.GetLogicalDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetName(b) == "Exportar HTML");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Settle(vault.Window, view);
        Assert.False(exported);
        return true;
    }, CancellationToken.None));
}
