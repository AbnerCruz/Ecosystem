using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Lunet.Core;

/// <summary>Uma referência para estudar mecânicas, não um asset ou um jogo distribuído pelo Lunet.</summary>
public sealed record InspirationReference(
    string Id, string Title, string Genre, string Perspective, string VisualStyle,
    string[] Mechanics, string StudyIdea, string Url = "", string ProjectName = "",
    string Notes = "", bool Favorite = false, bool Curated = false);

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
            if (loaded is null || loaded.Version != 1 || loaded.Entries is null)
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
                        ProjectName = Bounded(entry.ProjectName, 80, nameof(entry.ProjectName), allowEmpty: true)
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

    public IReadOnlyList<InspirationReference> All => _entries.OrderByDescending(e => e.Favorite)
        .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<string> Genres => Facets(e => new[] { e.Genre });
    public IReadOnlyList<string> Perspectives => Facets(e => new[] { e.Perspective });
    public IReadOnlyList<string> Styles => Facets(e => new[] { e.VisualStyle });
    public IReadOnlyList<string> Mechanics => Facets(e => e.Mechanics);

    private IReadOnlyList<string> Facets(Func<InspirationReference, IEnumerable<string>> selector) =>
        _entries.SelectMany(selector).Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<InspirationReference> Search(string? query = null, string? genre = null,
        string? mechanic = null, string? perspective = null, string? style = null, bool favoritesOnly = false)
    {
        var terms = Normalize(query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return All.Where(e => (!favoritesOnly || e.Favorite) &&
            ExactOrAll(e.Genre, genre) && ExactOrAll(e.Perspective, perspective) &&
            ExactOrAll(e.VisualStyle, style) &&
            (string.IsNullOrWhiteSpace(mechanic) || e.Mechanics.Any(m => ExactOrAll(m, mechanic))) &&
            terms.All(term => Normalize(string.Join(" ", new[] { e.Title, e.Genre, e.Perspective,
                e.VisualStyle, e.StudyIdea, e.Notes, e.ProjectName }.Concat(e.Mechanics))).Contains(term, StringComparison.Ordinal)))
            .ToArray();
    }

    private static bool ExactOrAll(string actual, string? wanted) =>
        string.IsNullOrWhiteSpace(wanted) || Normalize(actual) == Normalize(wanted);

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
        IEnumerable<string> mechanics, string studyIdea, string url = "", string projectName = "")
    {
        var entry = Validate(new InspirationReference(Guid.NewGuid().ToString("D"),
            title, genre, perspective, visualStyle, mechanics.ToArray(), studyIdea, url, projectName));
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

    public void SetPersonalDetails(string id, bool favorite, string notes, string projectName)
    {
        var current = _entries.FirstOrDefault(e => e.Id == id) ??
            throw new InvalidOperationException("Referência não encontrada.");
        var next = _entries.ToList();
        next[next.FindIndex(e => e.Id == id)] = current with
        {
            Favorite = favorite,
            Notes = Bounded(notes, 6000, "Notas", allowEmpty: true),
            ProjectName = Bounded(projectName, 80, "Projeto", allowEmpty: true)
        };
        Commit(next);
    }

    public void Delete(string id)
    {
        var item = _entries.FirstOrDefault(e => e.Id == id) ??
            throw new InvalidOperationException("Referência não encontrada.");
        if (item.Curated) throw new InvalidOperationException("Referências curadas não podem ser excluídas.");
        Commit(_entries.Where(e => e.Id != id).ToList());
    }

    private void Commit(List<InspirationReference> next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(new SavedState(1, next),
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
            Notes = Bounded(entry.Notes, 6000, "Notas", allowEmpty: true)
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
            new("ref." + id, title, genre, perspective, style, mechanics, idea, Curated: true);
        yield return Seed("stardew", "Stardew Valley", "Simulação", "Top-down", "Pixel art",
            "Estude como ciclos de dias, estações e rotinas dos personagens se combinam.", "Agricultura", "Calendário", "NPCs", "Progressão");
        yield return Seed("terraria", "Terraria", "Sandbox", "Lateral", "Pixel art",
            "Observe como exploração, criação de itens e progressão se conectam num mundo editável.", "Construção", "Crafting", "Exploração");
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
    }
}
