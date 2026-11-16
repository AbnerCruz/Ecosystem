using System.Text.Json;

namespace Lunet.Core;

/// <summary>
/// Preferências do launcher armazenadas fora dos projetos: nunca altera Game.cs, lunet.json ou assets.
/// Não guarda cópias de projetos. O ProjectStore continua sendo a única autoridade sobre quais existem.
/// </summary>
public sealed class LauncherPreferences
{
    private sealed record Document(int Version, string Section, string? LastProject, string[] PinnedProjects);

    private readonly string _path;
    private Document _state = new(1, "home", null, []);

    public LauncherPreferences(string path)
    {
        _path = Path.GetFullPath(path);
        if (!File.Exists(_path)) return;
        try
        {
            var data = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path))
                ?? throw new InvalidDataException("Preferências de início vazias.");
            if (data.Version != 1 || !ValidSection(data.Section) || data.PinnedProjects is null ||
                data.PinnedProjects.Any(n => !ValidName(n)) ||
                data.PinnedProjects.Distinct(StringComparer.Ordinal).Count() != data.PinnedProjects.Length ||
                (data.LastProject is not null && !ValidName(data.LastProject)))
                throw new InvalidDataException("Preferências de início inválidas. O arquivo original foi preservado.");
            _state = data;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Preferências de início danificadas. O arquivo original foi preservado.", ex);
        }
    }

    public string Section => _state.Section;
    public string? LastProject => _state.LastProject;
    public IReadOnlyList<string> PinnedProjects => _state.PinnedProjects.ToArray();

    public bool IsPinned(string name) => _state.PinnedProjects.Contains(name, StringComparer.Ordinal);

    public void SelectSection(string section)
    {
        if (!ValidSection(section)) throw new ArgumentException("Seção inválida.", nameof(section));
        if (_state.Section == section) return;
        Save(_state with { Section = section });
    }

    public void MarkOpened(string name)
    {
        RequireName(name);
        if (_state.LastProject == name) return;
        Save(_state with { LastProject = name });
    }

    public void SetPinned(string name, bool pinned)
    {
        RequireName(name);
        if (pinned == IsPinned(name)) return;
        var pins = pinned
            ? _state.PinnedProjects.Append(name).ToArray()
            : _state.PinnedProjects.Where(n => n != name).ToArray();
        Save(_state with { PinnedProjects = pins });
    }

    /// <summary>Remove somente os atalhos de um projeto excluído, nunca os arquivos do jogo.</summary>
    public void ForgetProject(string name)
    {
        RequireName(name);
        if (!IsPinned(name) && _state.LastProject != name) return;
        Save(_state with
        {
            LastProject = _state.LastProject == name ? null : _state.LastProject,
            PinnedProjects = _state.PinnedProjects.Where(n => n != name).ToArray()
        });
    }

    private void Save(Document next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
        _state = next; // Persist first: failed writes never produce a fictitious successful state.
    }

    private static bool ValidSection(string? section) => section is "home" or "projects" or "studios" or "explore";

    private static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 120 &&
        name == name.Trim() && name.IndexOfAny(['/', '\\', '\0']) < 0;

    private static void RequireName(string name)
    {
        if (!ValidName(name)) throw new ArgumentException("Nome de projeto inválido.", nameof(name));
    }
}
