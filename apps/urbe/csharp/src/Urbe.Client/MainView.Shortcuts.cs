using Urbe.Core;

namespace Urbe.Client;

/// <summary>UC-61: the keyboard shortcuts — the 1.8.4's, and the person's own in Personalização/atalhos.json over them.</summary>
public sealed partial class MainView
{
    private (string? Text, IReadOnlyDictionary<string, string> Map) _shortcuts = (null, new Dictionary<string, string>());

    /// <summary>The person's shortcuts, read again only when atalhos.json changed.</summary>
    private IReadOnlyDictionary<string, string> MyShortcuts()
    {
        var text = _contents.GetValueOrDefault(UserShortcuts.Path);
        if (!ReferenceEquals(text, _shortcuts.Text)) _shortcuts = (text, UserShortcuts.Parse(text));
        return _shortcuts.Map;
    }

    /// <summary>Configurações → Atalhos: every shortcut and its command; the last item opens atalhos.json (made from a model the first time).</summary>
    public Task ShowShortcutsAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var mine = MyShortcuts();
        string Title(string id) => Search.TitleOf(id) ?? id;
        var items = new List<DialogItem>();
        foreach (var (shortcut, command) in LegacyKeymap.List().Where(b => !mine.ContainsKey(b.Shortcut)))
            items.Add(new DialogItem(Pretty(shortcut), UrbeTheme.Icons.List, Title(command)));
        foreach (var (shortcut, command) in mine.OrderBy(p => p.Key, StringComparer.Ordinal))
            items.Add(new DialogItem(Pretty(shortcut), UrbeTheme.Icons.Pencil, command.Length == 0 ? "Desligado (atalhos.json)" : Title(command) + " (seu)"));
        items.Add(new DialogItem("Editar meus atalhos", UrbeTheme.Icons.Pencil, UserShortcuts.Path, Run: EditShortcutsAsync));
        return Dialogs.MenuAsync("Atalhos", items);
    }

    private static string Pretty(string shortcut) => shortcut.Replace("Mod+", "Ctrl+", StringComparison.Ordinal).Replace("ArrowLeft", "←", StringComparison.Ordinal).Replace("ArrowRight", "→", StringComparison.Ordinal);

    private async Task EditShortcutsAsync()
    {
        if (!_contents.ContainsKey(UserShortcuts.Path))
        {
            if (!VaultWritable) { await Dialogs.AlertAsync("Atalhos", NoVaultMessage); return; }
            try { await WriteVaultFileAsync(UserShortcuts.Path, UserShortcuts.Template + "\n", exists: false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                await Dialogs.AlertAsync("Atalhos", ex.Message);
                return;
            }
        }
        OpenNoteByPath(UserShortcuts.Path);
    }
}
