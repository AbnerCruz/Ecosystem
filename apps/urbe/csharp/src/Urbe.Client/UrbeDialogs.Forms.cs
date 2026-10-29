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

    /// <summary>
    /// CST-050: the template's form, with "Preencher no editor" besides Criar nota: the note is made from the answers
    /// given so far and the rest is written in the editor. InEditor tells which one the person chose; null answers, a cancel.
    /// </summary>
    public Task<(JsonObject? Answers, bool InEditor)> TemplateFormAsync(FormSession session, Action? changed = null, IReadOnlyCollection<string>? notes = null, IReadOnlyCollection<string>? folders = null) =>
        Open<(JsonObject?, bool)>(finish =>
    {
        Ui.FormRenderer? editor = null;
        void Save()
        {
            try { changed?.Invoke(); if (editor is not null) { editor.Status.Text = ""; editor.SubmitButton.IsEnabled = true; } }
            catch (IOException e) { if (editor is not null) { editor.Status.Text = e.Message; editor.SubmitButton.IsEnabled = false; } }
        }
        editor = new Ui.FormRenderer(session, Save, () => { Save(); if (editor!.SubmitButton.IsEnabled) finish((session.Answers(), false)); }, () => finish((null, false)), notes, folders);
        editor.SubmitButton.Content = "Criar nota";
        // no validation: what is missing is written in the note
        var inEditor = new Button { Content = "Preencher no editor", MinHeight = UrbeTokens.MinTouch, Padding = new Avalonia.Thickness(14, 8),
            CornerRadius = new Avalonia.CornerRadius(12), Background = Avalonia.Media.Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) };
        Avalonia.Automation.AutomationProperties.SetName(inEditor, "Preencher no editor");
        inEditor.Click += (_, _) => { Save(); finish((session.Answers(), true)); };
        if (editor.SubmitButton.Parent is WrapPanel buttons) buttons.Children.Insert(1, inEditor);
        return (editor, ((JsonObject?)null, false), null, this);
    });
}
