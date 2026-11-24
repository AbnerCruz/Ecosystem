using Android.App;
using Android.Content;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>Exploração offline de gêneros, mecânicas e referências, independente de qualquer projeto.</summary>
public sealed partial class MainActivity
{
    private InspirationLibrary? _inspirationLibrary;
    private bool _inspirationOpen;
    private string _inspirationQuery = "";
    private string[] _inspirationGenre = [];
    private string[] _inspirationMechanic = [];
    private string[] _inspirationPerspective = [];
    private string[] _inspirationStyle = [];
    private readonly HashSet<string> _inspirationComparison = new(StringComparer.Ordinal);
    private bool _inspirationViewLoaded;
    private string InspirationViewPath => System.IO.Path.Combine(FilesDir!.AbsolutePath, "inspiration-view.json");

    private void LoadInspirationView()
    {
        if (_inspirationViewLoaded) return;
        _inspirationViewLoaded = true;
        try
        {
            if (!File.Exists(InspirationViewPath)) return;
            var filter = System.Text.Json.JsonSerializer.Deserialize<InspirationFilter>(File.ReadAllText(InspirationViewPath));
            if (filter is null) return;
            _inspirationQuery = filter.Query ?? "";
            _inspirationGenre = filter.Genres ?? [];
            _inspirationMechanic = filter.Mechanics ?? [];
            _inspirationPerspective = filter.Perspectives ?? [];
            _inspirationStyle = filter.Styles ?? [];
            _inspirationFavorites = filter.FavoritesOnly;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            global::Android.Util.Log.Warn("Lunet.Explore", ex.Message);
        }
    }

    private InspirationFilter CurrentInspirationFilter() => new(_inspirationQuery, _inspirationGenre,
        _inspirationMechanic, _inspirationPerspective, _inspirationStyle, _inspirationFavorites);

    private void SaveInspirationView()
    {
        try
        {
            var path = InspirationViewPath;
            File.WriteAllText(path + ".tmp", System.Text.Json.JsonSerializer.Serialize(CurrentInspirationFilter()));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            global::Android.Util.Log.Warn("Lunet.Explore", ex.Message);
        }
    }
    private bool _inspirationFavorites;
    private const int InspirationPageSize = 18;
    private int _inspirationVisibleCount = InspirationPageSize;

    private void OpenInspirationExplorer()
    {
        try
        {
            _inspirationLibrary ??= new InspirationLibrary(
                System.IO.Path.Combine(FilesDir!.AbsolutePath, "inspiration.json"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // Never silently replace corrupt user data.
            new AlertDialog.Builder(this)!.SetTitle("Biblioteca indisponível")!
                .SetMessage(ex.Message + "\nSeus dados foram preservados.")!
                .SetPositiveButton("Fechar", (_, _) => { })!.Show();
            return;
        }

        LoadInspirationView();
        EnterLauncherSurface("explore");
        RefreshLauncherCatalog();
        _inspirationOpen = true;
        var library = _inspirationLibrary!;
        var root = Vertical();
        root.SetBackgroundColor(LunetUi.Background);
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(12));

        var top = LunetUi.Row(this, 8);
        top.AddView(LunetUi.Button(this, "← Início", ShowHome, ButtonStyle.Quiet),
            new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(48)));
        top.AddView(LunetUi.Label(this, "Explorar", LunetUi.TextTitle, LunetUi.Text, bold: true),
            LunetUi.Weight());
        top.AddView(LunetUi.Button(this, "＋ Referência", () => EditInspiration(null, OpenInspirationExplorer), ButtonStyle.Primary),
            new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(46)));
        root.AddView(top);
        root.AddView(LunetUi.Label(this, "Jogos, gêneros e mecânicas para estudar. Sem loja, sem assets de terceiros, sem depender de internet.",
            LunetUi.TextLabel, LunetUi.TextMuted));

        var search = LunetUi.Input(this, "Buscar jogos, mecânicas ou ideias", _inspirationQuery);
        root.AddView(search, LunetUi.FillWidth(Dp(48)));

        var filterStrip = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        var filterRow = LunetUi.Row(this, 6);
        filterRow.SetPadding(0, Dp(4), 0, Dp(4));
        filterStrip.AddView(filterRow);
        root.AddView(filterStrip);

        var toggles = LunetUi.Row(this, 12);
        var favorites = new CheckBox(this) { Text = "Apenas favoritos", Checked = _inspirationFavorites };
        favorites.SetTextColor(LunetUi.Text);
        toggles.AddView(favorites);
        toggles.AddView(LunetUi.Button(this, "Limpar filtros", () =>
        {
            _inspirationGenre = [];
            _inspirationMechanic = [];
            _inspirationPerspective = [];
            _inspirationStyle = [];
            _inspirationFavorites = false;
            _inspirationQuery = "";
            _inspirationVisibleCount = InspirationPageSize;
            _inspirationComparison.Clear();
            SaveInspirationView();
            OpenInspirationExplorer();
        }, ButtonStyle.Quiet));
        root.AddView(toggles);
        var compareBar = LunetUi.Row(this, 8);
        var compareButton = LunetUi.Button(this, "Comparar (0/2)", () => ShowInspirationComparison(), ButtonStyle.Secondary);
        compareBar.AddView(compareButton, LunetUi.Weight());
        compareBar.AddView(LunetUi.Button(this, "Arquivadas", () => ShowArchivedInspiration(OpenInspirationExplorer), ButtonStyle.Quiet));
        root.AddView(compareBar);

        var resultCount = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextFaint);
        root.AddView(resultCount);
        var items = LunetUi.Column(this, 10);
        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.AddView(items);
        root.AddView(scroll, Fill(1));

        void ShowProblem(Exception ex) =>
            Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();


        void Refresh()
        {
            int position = scroll.ScrollY;
            items.RemoveAllViews();
            var matches = library.SearchFacets(CurrentInspirationFilter());
            _inspirationComparison.RemoveWhere(id => !matches.Any(e => e.Id == id));
            compareButton.Text = $"Comparar ({_inspirationComparison.Count}/2)";
            compareButton.Enabled = _inspirationComparison.Count == 2;
            scroll.Post(() => scroll.ScrollTo(0, position));
            resultCount.Text = $"{matches.Count} referências · {library.All.Count} no catálogo";
            if (matches.Count == 0)
            {
                items.AddView(LunetUi.Label(this, "Nenhuma referência com estes filtros. Limpe os filtros ou crie uma referência pessoal.",
                    LunetUi.TextBody, LunetUi.TextMuted));
                return;
            }

            foreach (var reference in matches.Take(_inspirationVisibleCount))
            {
                var item = reference;
                var card = LunetUi.Column(this, 7);
                card.SetPadding(Dp(14), Dp(14), Dp(14), Dp(14));
                card.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
                var heading = LunetUi.Row(this, 8);
                heading.AddView(LunetUi.Label(this, item.Title, 16, LunetUi.Text, bold: true), LunetUi.Weight());
                heading.AddView(LunetUi.Chip(this, item.Favorite ? "★ Favorito" : "☆ Favoritar", () =>
                {
                    try
                    {
                        library.SetPersonalDetails(item.Id, !item.Favorite, item.Notes, item.ProjectName);
                        Refresh();
                    }
                    catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
                    { ShowProblem(ex); }
                }, item.Favorite));
                card.AddView(heading);
                card.AddView(LunetUi.Label(this,
                    $"{item.Genre} · {item.Perspective} · {item.VisualStyle}", LunetUi.TextLabel, LunetUi.AccentText));
                card.AddView(LunetUi.Label(this, "Mecânicas: " + string.Join(" · ", item.Mechanics),
                    LunetUi.TextLabel, LunetUi.TextMuted));
                card.AddView(LunetUi.Label(this, item.Curated ? "Catálogo Lunet" : "Referência pessoal", LunetUi.TextCaption, LunetUi.TextMuted));
                card.AddView(LunetUi.Label(this, item.StudyIdea, LunetUi.TextBody, LunetUi.Text));
                var selection = new CheckBox(this) { Text = "Selecionar para comparar", Checked = _inspirationComparison.Contains(item.Id) };
                selection.SetTextColor(LunetUi.Text);
                selection.CheckedChange += (_, args) =>
                {
                    if (args.IsChecked && !_inspirationComparison.Contains(item.Id) && _inspirationComparison.Count >= 2)
                    {
                        selection.Checked = false;
                        Toast.MakeText(this, "Desmarque uma referência antes de selecionar outra.", ToastLength.Short)?.Show();
                        return;
                    }
                    if (args.IsChecked) _inspirationComparison.Add(item.Id); else _inspirationComparison.Remove(item.Id);
                    compareButton.Text = $"Comparar ({_inspirationComparison.Count}/2)";
                    compareButton.Enabled = _inspirationComparison.Count == 2;
                };
                card.AddView(selection);
                if (!string.IsNullOrWhiteSpace(item.Notes))
                    card.AddView(LunetUi.Label(this, "Suas notas: " + item.Notes, LunetUi.TextLabel, LunetUi.TextMuted));
                var linkedFolder = InspirationLibrary.ResolveProject(item, _launcherCatalog);
                if (!string.IsNullOrWhiteSpace(item.ProjectName) || !string.IsNullOrWhiteSpace(item.ProjectId))
                    card.AddView(LunetUi.Label(this, linkedFolder is not null ? "Projeto associado: " + linkedFolder :
                        "Vínculo não resolvido: " + item.ProjectName + " (reassociar em Notas)",
                        LunetUi.TextCaption, LunetUi.TextMuted));

                var actions = LunetUi.Row(this, 6);
                actions.AddView(LunetUi.Button(this, "Ficha", () => ShowInspirationDetail(item, Refresh), ButtonStyle.Quiet));
                actions.AddView(LunetUi.Button(this, "Notas", () => EditInspirationNotes(item, Refresh),
                    ButtonStyle.Secondary));
                if (!item.Curated)
                {
                    actions.AddView(LunetUi.Button(this, "Editar", () => EditInspiration(item, OpenInspirationExplorer),
                        ButtonStyle.Quiet));
                    actions.AddView(LunetUi.Button(this, "Arquivar", () =>
                    {
                        new AlertDialog.Builder(this)!.SetTitle("Arquivar referência pessoal?")!
                            .SetMessage($"Arquivar \"{item.Title}\"? Suas notas e vínculos poderão ser recuperados.")!
                            .SetNegativeButton("Cancelar", (_, _) => { })!
                            .SetPositiveButton("Arquivar", (_, _) =>
                            {
                                try { library.Delete(item.Id); OpenInspirationExplorer(); }
                                catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
                                { ShowProblem(ex); }
                            })!.Show();
                    }, ButtonStyle.Danger));
                }
                card.AddView(actions);
                if (!string.IsNullOrWhiteSpace(item.Url))
                    card.AddView(LunetUi.Button(this, item.Curated ? "Pesquisar gameplay" : "Abrir página da referência", () =>
                    {
                        try
                        {
                            var intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(item.Url));
                            StartActivity(intent);
                        }
                        catch (ActivityNotFoundException)
                        {
                            Toast.MakeText(this, "Nenhum aplicativo disponível para abrir este link.", ToastLength.Long)?.Show();
                        }
                        catch (Java.Lang.SecurityException)
                        {
                            Toast.MakeText(this, "O Android bloqueou a abertura deste link.", ToastLength.Long)?.Show();
                        }
                    }, ButtonStyle.Quiet));
                if (linkedFolder is not null)
                    card.AddView(LunetUi.Button(this, "Abrir projeto associado", () =>
                    {
                        RefreshLauncherCatalog();
                        var folder = InspirationLibrary.ResolveProject(item, _launcherCatalog);
                        if (folder is not null) OpenProject(folder);
                        else Toast.MakeText(this, "Projeto não encontrado. O vínculo foi preservado.", ToastLength.Long)?.Show();
                    },
                        ButtonStyle.Quiet));
                items.AddView(card, LunetUi.FillWidth());
            }
            if (matches.Count > _inspirationVisibleCount)
            {
                items.AddView(LunetUi.Button(this,
                    $"Mostrar mais ({matches.Count - _inspirationVisibleCount} restantes)", () =>
                    {
                        _inspirationVisibleCount += InspirationPageSize;
                        Refresh();
                    }, ButtonStyle.Secondary));
            }
        }

        void AddFilter(string description, IReadOnlyList<string> values, string[] selected, Action<string[]> set)
        {
            var button = LunetUi.Button(this, description + $" ({selected.Length})", () =>
            {
                var chosen = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
                var list = LunetUi.Column(this, 4);
                var filterSearch = LunetUi.Input(this, "Buscar opções", "");
                var choices = LunetUi.Column(this, 2);
                void RenderOptions()
                {
                    choices.RemoveAllViews();
                    var query = filterSearch.Text ?? "";
                    foreach (var value in values.Where(v => v.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    {
                        var check = new CheckBox(this) { Text = value, Checked = chosen.Contains(value) };
                        check.SetTextColor(LunetUi.Text);
                        check.CheckedChange += (_, args) => { if (args.IsChecked) chosen.Add(value); else chosen.Remove(value); };
                        choices.AddView(check);
                    }
                }
                list.AddView(filterSearch);
                var optionsScroll = new ScrollView(this);
                optionsScroll.AddView(choices);
                list.AddView(optionsScroll, LunetUi.FillWidth(Dp(320)));
                filterSearch.TextChanged += (_, _) => RenderOptions();
                RenderOptions();
                new AlertDialog.Builder(this)!.SetTitle("Filtrar por " + description)!
                    .SetMessage(description == "Mecânicas" ? "Todas as mecânicas escolhidas devem existir." : "Qualquer opção escolhida nesta categoria.")!
                    .SetView(list)!
                    .SetNegativeButton("Cancelar", (_, _) => { })!
                    .SetNeutralButton("Limpar", (_, _) => { set([]); _inspirationVisibleCount = InspirationPageSize; SaveInspirationView(); OpenInspirationExplorer(); })!
                    .SetPositiveButton("Aplicar", (_, _) =>
                    {
                        set(chosen.ToArray());
                        _inspirationVisibleCount = InspirationPageSize;
                        SaveInspirationView();
                        OpenInspirationExplorer();
                    })!.Show();
            }, ButtonStyle.Secondary);
            filterRow.AddView(button, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(48)));
        }

        AddFilter("Gêneros", library.Genres, _inspirationGenre, value => _inspirationGenre = value);
        AddFilter("Mecânicas", library.Mechanics, _inspirationMechanic, value => _inspirationMechanic = value);
        AddFilter("Perspectivas", library.Perspectives, _inspirationPerspective, value => _inspirationPerspective = value);
        AddFilter("Estéticas", library.Styles, _inspirationStyle, value => _inspirationStyle = value);
        favorites.CheckedChange += (_, args) => { _inspirationFavorites = args.IsChecked; _inspirationVisibleCount = InspirationPageSize; SaveInspirationView(); Refresh(); };
        search.TextChanged += (_, _) => { _inspirationQuery = search.Text ?? ""; _inspirationVisibleCount = InspirationPageSize; SaveInspirationView(); Refresh(); };
        Refresh();
        SetLauncherContent(root, "explore", scroll);
    }

    private void EditInspirationNotes(InspirationReference reference, Action refresh)
    {
        var form = LunetUi.Column(this, 8);
        form.SetPadding(Dp(16), Dp(8), Dp(16), Dp(8));
        form.AddView(LunetUi.Label(this, "Anotações de estudo", LunetUi.TextLabel, LunetUi.TextMuted));
        var notes = LunetUi.Input(this, "O que quero aplicar no meu jogo?", reference.Notes, singleLine: false);
        notes.SetMinLines(4);
        form.AddView(notes);
        form.AddView(LunetUi.Label(this, "Associar a um projeto (opcional)", LunetUi.TextLabel, LunetUi.TextMuted));
        RefreshLauncherCatalog();
        var entries = _launcherCatalog.Where(e => LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, e.GameId) == e.FolderName).ToArray();
        var names = new[] { "Manter vínculo atual", "Sem projeto" }.Concat(entries.Select(e => e.FolderName + " · " + e.GameId[..8])).ToArray();
        var adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerItem, names);
        adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
        var project = new Spinner(this) { Adapter = adapter };
        project.SetSelection(0);
        form.AddView(project);
        if (!string.IsNullOrEmpty(reference.ProjectName))
            form.AddView(LunetUi.Label(this, "Vínculo registrado: " + reference.ProjectName,
                LunetUi.TextCaption, LunetUi.TextMuted));

        AlertDialog? dialog = null;
        form.AddView(LunetUi.Button(this, "Salvar notas", () =>
        {
            try
            {
                int selected = project.SelectedItemPosition;
                var chosen = selected >= 2 ? entries[selected - 2] : null;
                _inspirationLibrary!.SetPersonalDetails(reference.Id, reference.Favorite, notes.Text ?? "",
                    selected == 0 ? reference.ProjectName : chosen?.FolderName ?? "",
                    selected == 0 ? reference.ProjectId : chosen?.GameId ?? "");
                dialog?.Dismiss();
                refresh();
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
        }, ButtonStyle.Primary));
        dialog = new AlertDialog.Builder(this)!.SetTitle(reference.Title)!.SetView(form)!
            .SetNegativeButton("Cancelar", (_, _) => { })!.Create();
        dialog.Show();
    }

    private void EditInspiration(InspirationReference? original, Action refresh)
    {
        var form = LunetUi.Column(this, 7);
        form.SetPadding(Dp(16), Dp(8), Dp(16), Dp(8));
        EditText Field(string caption, string placeholder, string value, bool multiline = false)
        {
            form.AddView(LunetUi.Label(this, caption, LunetUi.TextLabel, LunetUi.TextMuted));
            var input = LunetUi.Input(this, placeholder, value, !multiline);
            if (multiline) input.SetMinLines(3);
            form.AddView(input);
            return input;
        }
        var title = Field("Título", "Nome do jogo ou referência", original?.Title ?? "");
        var genre = Field("Gênero", "Ex.: Puzzle", original?.Genre ?? "");
        var perspective = Field("Perspectiva", "Ex.: Top-down", original?.Perspective ?? "");
        var style = Field("Estilo visual", "Ex.: Pixel art", original?.VisualStyle ?? "");
        var mechanics = Field("Mecânicas (separadas por vírgulas)", "Ex.: voo, inventário, combate",
            original is null ? "" : string.Join(", ", original.Mechanics));
        var idea = Field("O que estudar", "Que decisões ou mecânicas merecem atenção?",
            original?.StudyIdea ?? "", multiline: true);
        var url = Field("Link (opcional, http ou https)", "https://...", original?.Url ?? "");
        var author = Field("Autoria (opcional)", "Autor ou fonte original", original?.Author ?? "");
        var license = Field("Licença (opcional)", "Licença conhecida; vazio se desconhecida", original?.License ?? "");
        var scroll = new ScrollView(this);
        var footer = LunetUi.Row(this, 8);
        AlertDialog? dialog = null;
        footer.AddView(LunetUi.Button(this, "Cancelar", () => dialog?.Dismiss(), ButtonStyle.Quiet));
        footer.AddView(LunetUi.Button(this, "Salvar referência", () =>
        {
            try
            {
                var tags = (mechanics.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);
                if (original is null)
                    _inspirationLibrary!.Add(title.Text ?? "", genre.Text ?? "",
                        perspective.Text ?? "", style.Text ?? "", tags, idea.Text ?? "", url.Text ?? "",
                        author: author.Text ?? "", license: license.Text ?? "");
                else
                    _inspirationLibrary!.Update(original with
                    {
                        Title = title.Text ?? "", Genre = genre.Text ?? "", Perspective = perspective.Text ?? "",
                        VisualStyle = style.Text ?? "", Mechanics = tags, StudyIdea = idea.Text ?? "",
                        Url = url.Text ?? "", Author = author.Text ?? "", License = license.Text ?? ""
                    });
                dialog?.Dismiss();
                refresh();
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            { Toast.MakeText(this, "Confira os campos: " + ex.Message, ToastLength.Long)?.Show(); }
        }, ButtonStyle.Primary));
        form.AddView(footer);
        scroll.AddView(form);
        dialog = new AlertDialog.Builder(this)!.SetTitle(original is null ? "Nova referência" : "Editar referência")!
            .SetView(scroll)!.Create();
        dialog.Show();
    }
}
