using System.Text.Json;

namespace Lunet.Core;

/// <summary>
/// Preferências do launcher armazenadas fora dos projetos: nunca altera Game.cs, lunet.json ou assets.
/// Não guarda cópias de projetos. O ProjectStore continua sendo a única autoridade sobre quais existem.
/// </summary>
public sealed class LauncherPreferences
{
    private sealed record Document(
        int Version, string Section, string? LastProject, string[] PinnedProjects,
        string? LastProjectId = null, string[]? PinnedProjectIds = null, string[]? RecentProjectIds = null);

    private readonly string _path;
    private Document _state = new(2, "home", null, [], null, [], []);

    public LauncherPreferences(string path)
    {
        _path = Path.GetFullPath(path);
        if (!File.Exists(_path)) return;
        try
        {
            var data = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path))
                ?? throw new InvalidDataException("Preferências de início vazias.");
            if (data.Version is not (1 or 2) || !ValidSection(data.Section) || data.PinnedProjects is null ||
                data.PinnedProjects.Any(n => !ValidName(n)) ||
                data.PinnedProjects.Distinct(StringComparer.Ordinal).Count() != data.PinnedProjects.Length ||
                (data.LastProject is not null && !ValidName(data.LastProject)) ||
                (data.LastProjectId is not null && !ValidGameId(data.LastProjectId)) ||
                (data.PinnedProjectIds is not null && (!ValidIds(data.PinnedProjectIds))) ||
                (data.RecentProjectIds is not null && (!ValidIds(data.RecentProjectIds) || data.RecentProjectIds.Length > 8)))
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
    public string? LastProjectId => _state.LastProjectId;
    public IReadOnlyList<string> PinnedProjectIds => (_state.PinnedProjectIds ?? []).ToArray();
    public IReadOnlyList<string> RecentProjectIds => (_state.RecentProjectIds ?? []).ToArray();
    public bool IsPinnedProject(string id) => (_state.PinnedProjectIds ?? [])
        .Contains(id, StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// Convert legacy name-only preferences when the actual manifest can be read.
    /// Unresolved names remain untouched for recovery; they do not become a guessed GameId.
    /// </summary>
    public void UpgradeProjectReferences(IReadOnlyList<LauncherProjectEntry> catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var mapped = _state.PinnedProjects
            .Select(name => LauncherProjectCatalog.UniqueIdForName(catalog, name))
            .Where(id => id is not null).Select(id => id!).ToArray();
        var pins = (_state.PinnedProjectIds ?? []).Concat(mapped)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var unmapped = _state.PinnedProjects
            .Where(name => LauncherProjectCatalog.UniqueIdForName(catalog, name) is null).ToArray();
        var last = _state.LastProjectId ??
            LauncherProjectCatalog.UniqueIdForName(catalog, _state.LastProject);
        var recent = (_state.RecentProjectIds ?? []).ToList();
        if (last is not null && !recent.Contains(last, StringComparer.OrdinalIgnoreCase))
            recent.Insert(0, last);
        recent = recent.Take(8).ToList();
        if (_state.Version == 2 && _state.LastProjectId == last &&
            _state.PinnedProjects.SequenceEqual(unmapped) &&
            (_state.PinnedProjectIds ?? []).SequenceEqual(pins) &&
            (_state.RecentProjectIds ?? []).SequenceEqual(recent))
            return;
        Save(_state with
        {
            Version = 2, LastProjectId = last, PinnedProjects = unmapped,
            PinnedProjectIds = pins, RecentProjectIds = recent.ToArray()
        });
    }

    public void MarkOpenedProject(string name, string gameId)
    {
        RequireName(name);
        var id = RequireGameId(gameId);
        var recent = new[] { id }.Concat((_state.RecentProjectIds ?? [])
            .Where(x => !string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
            .Take(8).ToArray();
        Save(_state with { Version = 2, LastProject = name, LastProjectId = id,
            RecentProjectIds = recent });
    }

    public void SetPinnedProject(string name, string gameId, bool pinned)
    {
        RequireName(name);
        var id = RequireGameId(gameId);
        var existing = _state.PinnedProjectIds ?? [];
        var current = existing.Contains(id, StringComparer.OrdinalIgnoreCase);
        if (current == pinned && !_state.PinnedProjects.Contains(name, StringComparer.Ordinal)) return;
        var ids = pinned ? existing.Append(id).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : existing.Where(x => !string.Equals(x, id, StringComparison.OrdinalIgnoreCase)).ToArray();
        Save(_state with { Version = 2, PinnedProjectIds = ids,
            PinnedProjects = _state.PinnedProjects.Where(x => x != name).ToArray() });
    }

    public void MovePinToStart(string name, string? gameId)
    {
        RequireName(name);
        if (gameId is null)
        {
            if (IsPinned(name) && _state.PinnedProjects[0] != name)
                Save(_state with { PinnedProjects = _state.PinnedProjects.Where(p => p != name).Prepend(name).ToArray() });
            return;
        }

        var id = RequireGameId(gameId);
        var pins = _state.PinnedProjectIds ?? [];
        if (!IsPinnedProject(id) || pins[0].Equals(id, StringComparison.OrdinalIgnoreCase)) return;
        Save(_state with { PinnedProjectIds = pins.Where(p => !p.Equals(id, StringComparison.OrdinalIgnoreCase)).Prepend(id).ToArray() });
    }

    public void ForgetProject(string name, string? gameId)
    {
        RequireName(name);
        if (gameId is null) { ForgetProject(name); return; }
        var id = RequireGameId(gameId);
        var clearLast = string.Equals(_state.LastProjectId, id, StringComparison.OrdinalIgnoreCase)
            || (_state.LastProjectId is null && _state.LastProject == name);
        Save(_state with
        {
            Version = 2,
            LastProject = clearLast ? null : _state.LastProject,
            LastProjectId = clearLast ? null : _state.LastProjectId,
            PinnedProjects = _state.PinnedProjects.Where(x => x != name).ToArray(),
            PinnedProjectIds = (_state.PinnedProjectIds ?? [])
                .Where(x => !string.Equals(x, id, StringComparison.OrdinalIgnoreCase)).ToArray(),
            RecentProjectIds = (_state.RecentProjectIds ?? [])
                .Where(x => !string.Equals(x, id, StringComparison.OrdinalIgnoreCase)).ToArray()
        });
    }

    private static bool ValidIds(string[] ids) =>
        ids.All(ValidGameId) && ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() == ids.Length;

    private static bool ValidGameId(string? id) => Guid.TryParse(id, out _);
    private static string RequireGameId(string id) =>
        Guid.TryParse(id, out var parsed) ? parsed.ToString("D")
            : throw new ArgumentException("GameId inválido.", nameof(id));

    private void Save(Document next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
        _state = next; // Persist first: failed writes never produce a fictitious successful state.
    }

    private static bool ValidSection(string? section) => section is "home" or "projects" or "studios" or "explore" or "learn";

    private static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 120 &&
        name == name.Trim() && name.IndexOfAny(['/', '\\', '\0']) < 0;

    private static void RequireName(string name)
    {
        if (!ValidName(name)) throw new ArgumentException("Nome de projeto inválido.", nameof(name));
    }
}
