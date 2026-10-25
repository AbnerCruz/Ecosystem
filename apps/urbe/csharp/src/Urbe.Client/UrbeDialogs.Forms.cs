using System.Text.Json.Nodes;
using Avalonia.Controls;
using Urbe.Core;
namespace Urbe.Client;
public sealed partial class UrbeDialogs
{
    public Task<JsonObject?> FormAsync(FormSession session, Action? changed = null, IReadOnlyCollection<string>? notes = null, IReadOnlyCollection<string>? folders = null) => Open<JsonObject?>(finish =>
    {
        Ui.FormRenderer? editor = null;
        void Save()
        {
            try { changed?.Invoke(); if (editor is not null) { editor.Status.Text = ""; editor.SubmitButton.IsEnabled = true; } }
            catch (IOException e) { if (editor is not null) { editor.Status.Text = e.Message; editor.SubmitButton.IsEnabled = false; } }
        }
        editor = new Ui.FormRenderer(session, Save, () => { Save(); if (editor!.SubmitButton.IsEnabled) finish(session.Answers()); }, () => finish(null), notes, folders);
        editor.SubmitButton.Content = "Criar nota";
        return (editor, null, null, this);
    });
}
