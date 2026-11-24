using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Lunet.Core;

/// <summary>Uma referência para estudar mecânicas, não um asset ou um jogo distribuído pelo Lunet.</summary>
public sealed record InspirationReference(
    string Id, string Title, string Genre, string Perspective, string VisualStyle,
    string[] Mechanics, string StudyIdea, string Url = "", string ProjectName = "",
    string Notes = "", bool Favorite = false, bool Curated = false,
    string ProjectId = "", string Author = "", string License = "", bool Archived = false);

/// <summary>Alternatives within genre, perspective and style; all selected mechanics are required.</summary>
public sealed record InspirationFilter(string Query = "", string[]? Genres = null, string[]? Mechanics = null,
    string[]? Perspectives = null, string[]? Styles = null, bool FavoritesOnly = false);

public sealed record InspirationComparison(InspirationReference First, InspirationReference Second,
    string[] SharedMechanics, string[] FirstOnly, string[] SecondOnly);

/// <summary>
/// Catálogo offline-first com referências curadas e referências pessoais. Os dados locais são a única fonte
/// de verdade; não existe serviço de rede, coleta de dados nem imagens de terceiros embutidas.
/// </summary>
public sealed class InspirationLibrary
{
    private sealed record SavedState(int Version, List<InspirationReference> Entries);
    private readonly string _path;
    private List<InspirationReference> _entries;

    public InspirationLibrary(string path)
    {
        _path = Path.GetFullPath(path);
        _entries = CuratedExamples().ToList();
        if (!File.Exists(_path)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(_path));
            if (loaded is null || loaded.Version is not (1 or 2) || loaded.Entries is null)
                throw new InvalidDataException("Formato da biblioteca de inspiração não reconhecido; o arquivo foi preservado.");
            var stored = loaded.Entries;
            if (stored.Any(e => e is null) || stored.GroupBy(e => e.Id, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new InvalidDataException("A biblioteca contém IDs repetidos ou registros inválidos; o arquivo foi preservado.");
            var byId = _entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
            foreach (var entry in stored)
            {
                if (byId.TryGetValue(entry.Id, out var reference))
                {
                    // The immutable curated metadata comes from the installed app; only personal fields travel.
                    byId[entry.Id] = reference with
                    {
                        Favorite = entry.Favorite,
                        Notes = Bounded(entry.Notes, 6000, nameof(entry.Notes), allowEmpty: true),
                        ProjectName = Bounded(entry.ProjectName, 80, nameof(entry.ProjectName), allowEmpty: true),
                        ProjectId = ValidProjectId(entry.ProjectId)
                    };
                }
                else
                {
                    if (entry.Curated && entry.Id.StartsWith("ref.", StringComparison.Ordinal))
                    {
                        // Keep retired curated entries as read-only snapshots so upgrades cannot erase
                        // user notes/favorites. If the ID returns to the catalog, installed metadata wins.
                        byId.Add(entry.Id, Validate(entry));
                    }
                    else
                    {
                        if (entry.Curated || !Guid.TryParse(entry.Id, out _))
                            throw new InvalidDataException("Referência salva sem identificação válida; o arquivo foi preservado.");
                        byId.Add(entry.Id, Validate(entry with { Curated = false }));
                    }
                }
            }
            _entries = byId.Values.ToList();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Não foi possível ler a biblioteca de inspiração. O arquivo original foi preservado.", ex);
        }
        catch (ArgumentException ex)
        {
            // Saved user data is untrusted input, even when the JSON itself is syntactically valid.
            // Convert validation failures to the documented recovery path instead of crashing Android.
            throw new InvalidDataException("A biblioteca contém campos inválidos. O arquivo original foi preservado.", ex);
        }
    }

    public IReadOnlyList<InspirationReference> All => _entries.Where(e => !e.Archived).OrderByDescending(e => e.Favorite)
        .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<InspirationReference> Archived => _entries.Where(e => e.Archived)
        .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<string> Genres => Facets(e => new[] { e.Genre });
    public IReadOnlyList<string> Perspectives => Facets(e => new[] { e.Perspective });
    public IReadOnlyList<string> Styles => Facets(e => new[] { e.VisualStyle });
    public IReadOnlyList<string> Mechanics => Facets(e => e.Mechanics);

    private IReadOnlyList<string> Facets(Func<InspirationReference, IEnumerable<string>> selector) =>
        _entries.Where(e => !e.Archived).SelectMany(selector).Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<InspirationReference> Search(string? query = null, string? genre = null,
        string? mechanic = null, string? perspective = null, string? style = null, bool favoritesOnly = false)
    {
        return SearchFacets(new InspirationFilter(query ?? "", One(genre), One(mechanic), One(perspective), One(style), favoritesOnly));
    }

    private static string[] One(string? value) => string.IsNullOrWhiteSpace(value) ? [] : [value];

    public IReadOnlyList<InspirationReference> SearchFacets(InspirationFilter filter)
    {
        var terms = Normalize(filter.Query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var genres = NormalizeSet(filter.Genres);
        var mechanics = NormalizeSet(filter.Mechanics);
        var perspectives = NormalizeSet(filter.Perspectives);
        var styles = NormalizeSet(filter.Styles);
        return All.Where(e =>
        {
            if ((filter.FavoritesOnly && !e.Favorite) ||
                !Matches(e.Genre, genres) || !Matches(e.Perspective, perspectives) || !Matches(e.VisualStyle, styles))
                return false;
            var tags = NormalizeSet(e.Mechanics);
            if (!mechanics.IsSubsetOf(tags)) return false;
            var haystack = Normalize(string.Join(" ", new[] { e.Title, e.Genre, e.Perspective,
                e.VisualStyle, e.StudyIdea, e.Notes, e.ProjectName, e.Author, e.License }.Concat(e.Mechanics)));
            return terms.All(term => haystack.Contains(term, StringComparison.Ordinal));
        }).ToArray();
    }

    private static HashSet<string> NormalizeSet(IEnumerable<string>? values) =>
        (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => Normalize(v.Trim()))
            .ToHashSet(StringComparer.Ordinal);

    private static bool Matches(string value, HashSet<string> wanted) =>
        wanted.Count == 0 || wanted.Contains(Normalize(value));

    public static string? ResolveProject(InspirationReference reference, IEnumerable<LauncherProjectEntry> catalog) =>
        LauncherProjectCatalog.UniqueNameForId(catalog, reference.ProjectId);

    public InspirationComparison Compare(string firstId, string secondId)
    {
        if (firstId == secondId) throw new ArgumentException("Selecione duas referências distintas.");
        var first = All.FirstOrDefault(e => e.Id == firstId) ?? throw new InvalidOperationException("Primeira referência ausente.");
        var second = All.FirstOrDefault(e => e.Id == secondId) ?? throw new InvalidOperationException("Segunda referência ausente.");
        var a = NormalizeSet(first.Mechanics);
        var b = NormalizeSet(second.Mechanics);
        return new InspirationComparison(first, second,
            first.Mechanics.Where(t => b.Contains(Normalize(t))).ToArray(),
            first.Mechanics.Where(t => !b.Contains(Normalize(t))).ToArray(),
            second.Mechanics.Where(t => !a.Contains(Normalize(t))).ToArray());
    }

    /// <summary>Original proposed study activity, not an assertion about a commercial game's implementation.</summary>
    public static string SuggestExperiment(InspirationReference reference, string? mechanic = null)
    {
        var selected = reference.Mechanics.FirstOrDefault(m => Normalize(m) == Normalize(mechanic ?? "")) ?? reference.Mechanics.First();
        var tag = Normalize(selected);
        var design = tag switch
        {
            "dash" => "Implemente duas distâncias de dash e meça tempo de recuperação e erros de posicionamento.",
            "voo" or "montarias" => "Faça uma área de teste com decolagem, controle e pouso. Compare duas velocidades e registre colisões.",
            "combate" => "Prototipe um ataque com aviso visual e compare duas durações do aviso.",
            "turnos" => "Modele três turnos e compare ordem fixa e ordem alternada. Registre decisões e erros.",
            "construcao" or "crafting" => "Prototipe duas receitas ou peças com custos diferentes e registre tempo e escolhas.",
            "plataforma de precisao" or "respawn" => "Crie um obstáculo curto e compare dois tempos de retomada após falhar.",
            "ia de agentes" => "Implemente duas prioridades concorrentes e registre a escolha em cinco situações.",
            _ => "Crie um protótipo mínimo com esta mecânica, altere uma variável por vez e registre o resultado de duas tentativas."
        };
        return selected + " — " + design + " Use código e assets próprios.";
    }

    private static string ValidProjectId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";
        if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty)
            throw new ArgumentException("GameId inválido.");
        return parsed.ToString("D");
    }

    private static string Normalize(string value)
    {
        var text = value.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            buffer.Append(char.ToLowerInvariant(c));
        }
        return buffer.ToString().Normalize(NormalizationForm.FormC);
    }

    public InspirationReference Add(string title, string genre, string perspective, string visualStyle,
        IEnumerable<string> mechanics, string studyIdea, string url = "", string projectName = "",
        string author = "", string license = "")
    {
        var entry = Validate(new InspirationReference(Guid.NewGuid().ToString("D"),
            title, genre, perspective, visualStyle, mechanics.ToArray(), studyIdea, url, projectName, Author: author, License: license));
        Commit(_entries.Append(entry).ToList());
        return entry;
    }

    public InspirationReference Update(InspirationReference entry)
    {
        int index = _entries.FindIndex(e => e.Id == entry.Id);
        if (index < 0) throw new InvalidOperationException("Referência não encontrada.");
        if (_entries[index].Curated)
            throw new InvalidOperationException("Referências curadas não podem ser reescritas; edite apenas suas notas.");
        var validated = Validate(entry with { Curated = false });
        var next = _entries.ToList();
        next[index] = validated;
        Commit(next);
        return validated;
    }

    public void SetPersonalDetails(string id, bool favorite, string notes, string projectName, string? projectId = null)
    {
        var current = _entries.FirstOrDefault(e => e.Id == id) ??
            throw new InvalidOperationException("Referência não encontrada.");
        var next = _entries.ToList();
        next[next.FindIndex(e => e.Id == id)] = current with
        {
            Favorite = favorite,
            Notes = Bounded(notes, 6000, "Notas", allowEmpty: true),
            ProjectName = Bounded(projectName, 80, "Projeto", allowEmpty: true),
            ProjectId = projectId is null && projectName == current.ProjectName ? current.ProjectId : ValidProjectId(projectId)
        };
        Commit(next);
    }

    public void Delete(string id)
    {
        var item = _entries.FirstOrDefault(e => e.Id == id) ??
            throw new InvalidOperationException("Referência não encontrada.");
        if (item.Curated) throw new InvalidOperationException("Referências curadas não podem ser excluídas.");
        // Explicit deletion is recoverable; retain the note, attribution and stable project link.
        Commit(_entries.Select(e => e.Id == id ? e with { Archived = true } : e).ToList());
    }

    public void Restore(string id)
    {
        if (!_entries.Any(e => e.Id == id && e.Archived && !e.Curated))
            throw new InvalidOperationException("Referência arquivada não encontrada.");
        Commit(_entries.Select(e => e.Id == id ? e with { Archived = false } : e).ToList());
    }

    private void Commit(List<InspirationReference> next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(new SavedState(2, next),
            new JsonSerializerOptions { WriteIndented = true }));
        _entries = next; // Never report success on a failed disk write.
    }

    private static InspirationReference Validate(InspirationReference entry)
    {
        if (entry.Mechanics is null || entry.Mechanics.Length is < 1 or > 12)
            throw new ArgumentException("Informe de 1 a 12 mecânicas.");
        var mechanics = entry.Mechanics.Select(m => Bounded(m, 60, "Mecânica")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var url = Bounded(entry.Url, 1024, "Link", allowEmpty: true);
        if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("http" or "https")))
            throw new ArgumentException("O link deve usar https:// ou http://.");
        return entry with
        {
            Title = Bounded(entry.Title, 120, "Título"),
            Genre = Bounded(entry.Genre, 80, "Gênero"),
            Perspective = Bounded(entry.Perspective, 80, "Perspectiva"),
            VisualStyle = Bounded(entry.VisualStyle, 80, "Estética"),
            Mechanics = mechanics,
            StudyIdea = Bounded(entry.StudyIdea, 1000, "O que estudar"),
            Url = url,
            ProjectName = Bounded(entry.ProjectName, 80, "Projeto", allowEmpty: true),
            Notes = Bounded(entry.Notes, 6000, "Notas", allowEmpty: true),
            ProjectId = ValidProjectId(entry.ProjectId),
            Author = Bounded(entry.Author, 200, "Autoria", allowEmpty: true),
            License = Bounded(entry.License, 200, "Licença", allowEmpty: true)
        };
    }

    private static string Bounded(string? value, int max, string field, bool allowEmpty = false)
    {
        var result = (value ?? "").Trim();
        if (result.Length > max || (!allowEmpty && result.Length == 0))
            throw new ArgumentException($"{field}: use de {(allowEmpty ? 0 : 1)} a {max} caracteres.");
        return result;
    }

    private static IEnumerable<InspirationReference> CuratedExamples()
    {
        static InspirationReference Seed(string id, string title, string genre, string perspective,
            string style, string idea, params string[] mechanics) =>
            new("ref." + id, title, genre, perspective, style, mechanics, idea,
                InspirationCatalogExpansion.GameplaySearchUrl(title), Curated: true);
        yield return Seed("stardew", "Stardew Valley", "Simulação", "Top-down", "Pixel art",
            "Estude como ciclos de dias, estações e rotinas dos personagens se combinam.", "Agricultura", "Calendário", "NPCs", "Progressão");
        yield return Seed("terraria", "Terraria", "Sandbox", "Lateral", "Pixel art",
            "Observe como exploração, crafting, montarias voadoras e progressão se conectam num mundo editável.", "Construção", "Crafting", "Exploração", "Montarias", "Voo");
        yield return Seed("celeste", "Celeste", "Plataforma", "Lateral", "Pixel art",
            "Analise precisão de movimento, leitura da fase e retomada rápida após erros.", "Dash", "Plataforma de precisão", "Respawn");
        yield return Seed("survivors", "Vampire Survivors", "Roguelite", "Top-down", "Pixel art",
            "Compare a tomada de decisões sobre melhorias com a simplicidade dos controles.", "Autoataque", "Sobrevivência", "Progressão");
        yield return Seed("baba", "Baba Is You", "Puzzle", "Top-down", "Pixel art",
            "Investigue regras que são objetos manipuláveis no próprio tabuleiro.", "Regras dinâmicas", "Empurrar blocos", "Lógica");
        yield return Seed("breach", "Into the Breach", "Estratégia", "Grade tática", "Pixel art",
            "Estude como mostrar antecipadamente ataques inimigos cria decisões táticas legíveis.", "Turnos", "Posicionamento", "Intenção inimiga");
        yield return Seed("rimworld", "RimWorld", "Simulação", "Top-down", "2D estilizado",
            "Observe prioridades autônomas e como eventos mudam histórias emergentes.", "IA de agentes", "Construção", "Eventos");
        yield return Seed("factorio", "Factorio", "Automação", "Top-down", "2D detalhado",
            "Analise fluxos de recursos, gargalos e feedback da produção.", "Logística", "Automação", "Crafting");
        yield return Seed("minimetro", "Mini Metro", "Puzzle", "Mapa esquemático", "Minimalista",
            "Investigue design de linhas, demanda crescente e clareza de informação.", "Rotas", "Gerenciamento", "Otimização");
        yield return Seed("slay", "Slay the Spire", "Estratégia", "Cartas 2D", "Ilustrado",
            "Estude combinações de cartas e risco/recompensa durante a progressão.", "Deckbuilding", "Turnos", "Roguelite");
        yield return Seed("hyperlight", "Hyper Light Drifter", "Ação e aventura", "Top-down", "Pixel art",
            "Observe como ambientação e exploração orientam o jogador com pouco texto.", "Combate", "Exploração", "Dash");
        yield return Seed("ftl", "FTL: Faster Than Light", "Estratégia", "Vista de sistemas", "2D estilizado",
            "Analise gestão de tripulação, sistemas interdependentes e decisões em tempo real pausável.",
            "Gerenciamento", "Roguelite", "Combate tático");
        foreach (var reference in InspirationCatalogExpansion.Entries())
            yield return reference;
    }
}
