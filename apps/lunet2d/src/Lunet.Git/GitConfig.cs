namespace Lunet.Git;

/// <summary>Arquivo .git/config: seções [nome "sub"] com chave = valor. Suporta o necessário (remotos, ramos, usuário).</summary>
public sealed class GitConfig
{
    private readonly string _path;
    private readonly List<(string Section, string? Subsection, string Key, string Value)> _values = [];

    public GitConfig(string path)
    {
        _path = path;
        if (!File.Exists(path)) return;
        string section = "";
        string? subsection = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';') continue;
            if (line[0] == '[' && line[^1] == ']')
            {
                var inner = line[1..^1].Trim();
                var quote = inner.IndexOf('"');
                if (quote > 0)
                {
                    section = inner[..quote].Trim().ToLowerInvariant();
                    subsection = inner[(quote + 1)..inner.LastIndexOf('"')];
                }
                else
                {
                    section = inner.ToLowerInvariant();
                    subsection = null;
                }
                continue;
            }
            var equals = line.IndexOf('=');
            var key = (equals < 0 ? line : line[..equals]).Trim().ToLowerInvariant();
            var value = equals < 0 ? "true" : line[(equals + 1)..].Trim().Trim('"');
            _values.Add((section, subsection, key, value));
        }
    }

    public string? Get(string section, string? subsection, string key) =>
        _values.LastOrDefault(v => v.Section == section.ToLowerInvariant() && v.Subsection == subsection && v.Key == key.ToLowerInvariant()).Value;

    public void Set(string section, string? subsection, string key, string value)
    {
        _values.RemoveAll(v => v.Section == section.ToLowerInvariant() && v.Subsection == subsection && v.Key == key.ToLowerInvariant());
        _values.Add((section.ToLowerInvariant(), subsection, key.ToLowerInvariant(), value));
    }

    public IEnumerable<string> Subsections(string section) =>
        _values.Where(v => v.Section == section.ToLowerInvariant() && v.Subsection is not null).Select(v => v.Subsection!).Distinct();

    public void RemoveSection(string section, string? subsection) =>
        _values.RemoveAll(v => v.Section == section.ToLowerInvariant() && v.Subsection == subsection);

    public void Save()
    {
        var lines = new List<string>();
        foreach (var group in _values.GroupBy(v => (v.Section, v.Subsection)))
        {
            lines.Add(group.Key.Subsection is null ? $"[{group.Key.Section}]" : $"[{group.Key.Section} \"{group.Key.Subsection}\"]");
            foreach (var value in group) lines.Add($"\t{value.Key} = {value.Value}");
        }
        File.WriteAllText(_path, string.Join('\n', lines) + "\n");
    }
}
