using Avalonia.Controls;

namespace Urbe.Client;

/// <summary>
/// The native plugin API of the Urbe (owner, ADD-0021/DEC-0044: "API de Plugin nativo"). A plugin is C# code with a
/// manifest that declares what it may do; the host refuses everything else (deny-by-default, NN-016) and undoes all
/// a plugin registered when it is turned off, as the 1.8.4's plugins.js did ("tudo limpo ao desligar"). The 1.8.4's
/// .js plugins are not run. The API mirrors what plugins.js offered: notices, buttons, commands, notes, events,
/// panels and the plugin's own data.
/// </summary>
public static class PluginPermissions
{
    public const string Notices = "avisos";
    public const string Buttons = "botoes";
    public const string Commands = "comandos";
    public const string ReadNotes = "notas.ler";
    public const string WriteNotes = "notas.escrever";
    public const string Events = "eventos";
    public const string Panels = "paineis";
    public const string Data = "dados";

    /// <summary>What each permission lets the plugin do, as the Personalização shows it.</summary>
    public static IReadOnlyDictionary<string, string> Labels { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Notices] = "Mostrar avisos",
        [Buttons] = "Pôr botões no topo das notas",
        [Commands] = "Acrescentar comandos",
        [ReadNotes] = "Ler suas notas",
        [WriteNotes] = "Criar e mudar notas",
        [Events] = "Saber quando notas abrem e são salvas",
        [Panels] = "Abrir painéis sobre as notas e o editor",
        [Data] = "Guardar dados próprios neste aparelho",
    };
}

/// <summary>Who the plugin is and what it may do.</summary>
public sealed record PluginManifest(string Id, string Name, string Version, string Description, IReadOnlyList<string> Permissions);

/// <summary>A native plugin: started with its API when turned on, stopped (and cleaned up by the host) when turned off.</summary>
public interface IUrbePlugin
{
    PluginManifest Manifest { get; }
    void Start(IPluginApi api);
    void Stop() { }
}

/// <summary>What a plugin can ask of the Urbe. Each call needs the permission its manifest declared; otherwise it throws.</summary>
public interface IPluginApi
{
    string PluginId { get; }

    /// <summary><see cref="PluginPermissions.Notices"/>: the discreet notice over the app.</summary>
    void Notice(string text);

    /// <summary><see cref="PluginPermissions.Buttons"/>: a button in the Explorer's header (before Configurações).</summary>
    void Button(string title, string icon, Func<Task> run);

    /// <summary><see cref="PluginPermissions.Commands"/>: a command in the palette (Abrir comandos), under "Plugin · name".</summary>
    void Command(string title, Func<Task> run);

    /// <summary><see cref="PluginPermissions.ReadNotes"/>: the notes' paths (in a folder, or all).</summary>
    IReadOnlyList<string> Notes(string? folder = null);

    /// <summary><see cref="PluginPermissions.ReadNotes"/>: a note's text, or null.</summary>
    string? ReadNote(string path);

    /// <summary><see cref="PluginPermissions.WriteNotes"/>: creates or saves a note (never in .urbe or the plugins folder).</summary>
    Task WriteNoteAsync(string path, string content);

    /// <summary><see cref="PluginPermissions.Events"/>: "nota:aberta" or "nota:salva", with the note's path.</summary>
    void On(string evt, Action<string> handler);

    /// <summary><see cref="PluginPermissions.Panels"/>: a floating panel over the Explorer and the editor; shown or hidden by the plugin.</summary>
    IPluginPanel Panel(string title, Control content);

    /// <summary><see cref="PluginPermissions.Data"/>: the plugin's own value kept on this device.</summary>
    string? GetData(string key);

    /// <summary><see cref="PluginPermissions.Data"/>: keeps (or with null, removes) the plugin's own value on this device.</summary>
    void SetData(string key, string? value);

    /// <summary><see cref="PluginPermissions.Buttons"/>: a button in the editor's top bar.</summary>
    void EditorButton(string title, string icon, Func<Task> run);

    /// <summary><see cref="PluginPermissions.WriteNotes"/>: text at the caret of the note open in the editor.</summary>
    void InsertText(string text);

    /// <summary><see cref="PluginPermissions.ReadNotes"/>: opens a note in the editor.</summary>
    void OpenNote(string path);

    /// <summary>The note open in the editor, or null.</summary>
    string? OpenNotePath { get; }
}

/// <summary>A plugin's floating panel.</summary>
public interface IPluginPanel
{
    bool IsOpen { get; }
    void Show();
    void Hide();
    void Toggle();
}

/// <summary>The native plugins that ship with the Urbe (first-party).</summary>
public static class BuiltInPlugins
{
    public static IReadOnlyList<Func<IUrbePlugin>> All { get; } = [() => new Plugins.ClockPlugin()];
}
