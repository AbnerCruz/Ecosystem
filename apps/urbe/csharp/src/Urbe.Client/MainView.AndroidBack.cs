namespace Urbe.Client;

/// <summary>
/// Android system Back follows the same hierarchy as on-screen Back controls. Never discard an unsaved
/// document or terminate the process while a modal/panel is present; the native Activity owns root exit.
/// </summary>
public sealed partial class MainView
{
    public bool HandleSystemBack()
    {
        if (Dialogs.IsOpen) { Dialogs.Cancel(); return true; }
        if (ActionSheet.IsOpen) { _ = ActionSheet.CloseAsync(); return true; }
        if (Formula.IsVisible) { Formula.IsVisible = false; return true; }
        if (Customize.IsVisible) { Customize.Close(); return true; }
        if (_assistant is { IsVisible: true } panel) return panel.HandleBack();
        if (_studio is { IsVisible: true } studio) { _ = studio.CloseAsync(); return true; }
        if (FilePreview.IsVisible) { FilePreview.Close(); return true; }
        if (_sky is { IsVisible: true } sky) { sky.IsVisible = false; return true; }
        if (GraphOpen) { _ = CloseGraphAsync(); return true; }
        if (_pagesPanel is { IsVisible: true } pages) { pages.Close(); return true; }
        if (NotesOverEditor) { CloseNotesOverEditor(); return true; }
        if (Editor.IsVisible) { _ = Editor.BackAsync(); return true; }
        // CST-010: inside a project of the Área de trabalho, Voltar goes up before it leaves the app
        if (Workspace.IsEffectivelyVisible && Workspace.Back()) return true;
        return false;
    }
}
