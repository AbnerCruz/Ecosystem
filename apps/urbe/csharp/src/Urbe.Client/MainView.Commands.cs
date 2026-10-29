using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The rest of the 1.8.4's commands (core.commands): the editor's session (editor.session.restore, kept on this device
/// as editor/workspace.js kept it in localStorage), Abrir tema.json (customize.edit-file) and the helpers the palette
/// and the keyboard shortcuts call.
/// </summary>
public sealed partial class MainView
{
    private const string SessionKey = "urbe.editor.workspace.v1";

    private void WatchSession()
    {
        Editor.Shown += _ => SaveSession();
        Editor.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) SaveSession(); };
    }

    /// <summary>persist(): the open tabs and the active note, on this device.</summary>
    private void SaveSession()
    {
        // the note shown before the folder is read is not a session of its own (nor is a vault not open yet)
        if (_vault is not { IsConnected: true } || Editor.Tabs.Count == 0 || Editor.ShowingEarly) return;
        var o = new JsonObject
        {
            ["tabs"] = new JsonArray(Editor.Tabs.Select(t => (JsonNode)JsonValue.Create(t)).ToArray()),
            ["activeId"] = Editor.IsVisible ? Editor.Current?.Path : null,
            ["vault"] = _vault.VaultName
        };
        Customization.Device.Set(SessionKey, o.ToJsonString());
    }

    /// <summary>The last session's active note, read alone and shown while the folder is read (nothing when there is none).</summary>
    private async Task ShowLastNoteEarlyAsync()
    {
        try
        {
            if (_vault is null || Customization.Device.Get(SessionKey) is not { } text || JsonNode.Parse(text) is not JsonObject o) return;
            if ((string?)o["activeId"] is not { Length: > 0 } active) return;
            if (OpeningNames.Read() is { } names && (string?)o["vault"] is { } vault && names.Vault != vault) return;
            if (await _vault.PeekTextAsync(active) is not { } content) return;
            Editor.ShowEarly(new CityNote(active, content));
        }
        catch (JsonException) { }
    }

    /// <summary>editor.session.restore: reopens the tabs of the last session of this vault.</summary>
    public bool RestoreSession()
    {
        try
        {
            if (Customization.Device.Get(SessionKey) is not { } text || JsonNode.Parse(text) is not JsonObject o) return false;
            if ((string?)o["vault"] != _vault?.VaultName) return false;
            var tabs = (o["tabs"] as JsonArray ?? []).Select(x => (string?)x).OfType<string>().Where(_contents.ContainsKey).ToList();
            if (tabs.Count == 0) return false;
            Editor.RestoreSession(tabs, (string?)o["activeId"]);
            return true;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>customize.edit-file: Personalização/tema.json in the editor (written first when the vault has none).</summary>
    public async Task EditThemeFileAsync()
    {
        if (!_contents.ContainsKey(LegacyCustomize.FilePath))
        {
            if (!VaultWritable) { await Dialogs.AlertAsync("Abrir tema.json", NoVaultMessage); return; }
            await Customization.SaveAsync(Customization.Config);
            if (!_contents.ContainsKey(LegacyCustomize.FilePath)) await WriteVaultFileAsync(LegacyCustomize.FilePath, "{\n  \"$schema\": \"urbe-tema-1\"\n}\n", exists: false);
        }
        OpenNoteByPath(LegacyCustomize.FilePath);
    }
}
