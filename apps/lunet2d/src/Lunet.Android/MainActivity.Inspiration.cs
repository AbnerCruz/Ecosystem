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
    private string _inspirationGenre = "";
    private string _inspirationMechanic = "";
    private string _inspirationPerspective = "";
    private string _inspirationStyle = "";
    private bool _inspirationFavorites;

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

        EnterLauncherSurface("explore");
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
            _inspirationGenre = "";
            _inspirationMechanic = "";
            _inspirationPerspective = "";
            _inspirationStyle = "";
            _inspirationFavorites = false;
            _inspirationQuery = "";
            OpenInspirationExplorer();
        }, ButtonStyle.Quiet));
        root.AddView(toggles);

        var resultCount = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextFaint);
        root.AddView(resultCount);
        var items = LunetUi.Column(this, 10);
        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.AddView(items);
        root.AddView(scroll, Fill(1));

        void ShowProblem(Exception ex) =>
            Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();

        const int pageSize = 18;
        int visibleCount = pageSize;

        void Refresh()
        {
            items.RemoveAllViews();
            var matches = library.Search(_inspirationQuery, _inspirationGenre, _inspirationMechanic,
                _inspirationPerspective, _inspirationStyle, _inspirationFavorites);
            resultCount.Text = $"{matches.Count} referências · {library.All.Count} no catálogo";
            if (matches.Count == 0)
            {
                items.AddView(LunetUi.Label(this, "Nenhuma referência com estes filtros. Limpe os filtros ou crie uma referência pessoal.",
                    LunetUi.TextBody, LunetUi.TextMuted));
                return;
            }

            foreach (var reference in matches.Take(visibleCount))
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
                card.AddView(LunetUi.Label(this, item.StudyIdea, LunetUi.TextBody, LunetUi.Text));
                if (!string.IsNullOrWhiteSpace(item.Notes))
                    card.AddView(LunetUi.Label(this, "Suas notas: " + item.Notes, LunetUi.TextLabel, LunetUi.TextMuted));
                if (!string.IsNullOrWhiteSpace(item.ProjectName))
                    card.AddView(LunetUi.Label(this, "Projeto associado: " + item.ProjectName,
                        LunetUi.TextCaption, LunetUi.TextMuted));

                var actions = LunetUi.Row(this, 6);
                actions.AddView(LunetUi.Button(this, "Notas", () => EditInspirationNotes(item, Refresh),
                    ButtonStyle.Secondary));
                if (!item.Curated)
                {
                    actions.AddView(LunetUi.Button(this, "Editar", () => EditInspiration(item, OpenInspirationExplorer),
                        ButtonStyle.Quiet));
                    actions.AddView(LunetUi.Button(this, "Excluir", () =>
                    {
                        new AlertDialog.Builder(this)!.SetTitle("Excluir referência pessoal?")!
                            .SetMessage($"Excluir \"{item.Title}\" da sua biblioteca?")!
                            .SetNegativeButton("Cancelar", (_, _) => { })!
                            .SetPositiveButton("Excluir", (_, _) =>
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
                if (!string.IsNullOrWhiteSpace(item.ProjectName) &&
                    _store.List().Contains(item.ProjectName, StringComparer.Ordinal))
                    card.AddView(LunetUi.Button(this, "Abrir projeto associado", () => OpenProject(item.ProjectName),
                        ButtonStyle.Quiet));
                items.AddView(card, LunetUi.FillWidth());
            }
            if (matches.Count > visibleCount)
            {
                items.AddView(LunetUi.Button(this,
                    $"Mostrar mais ({matches.Count - visibleCount} restantes)", () =>
                    {
                        visibleCount += pageSize;
                        Refresh();
                    }, ButtonStyle.Secondary));
            }
        }

        Spinner AddFilter(string description, IReadOnlyList<string> values, string selected, Action<string> set)
        {
            var options = new[] { "Todos · " + description }.Concat(values).ToArray();
            var adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerItem, options);
            adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
            var spinner = new Spinner(this) { Adapter = adapter };
            spinner.ContentDescription = "Filtrar por " + description;
            filterRow.AddView(spinner, new LinearLayout.LayoutParams(Dp(175), Dp(48)));
            int index = Array.FindIndex(options, x => x.Equals(selected, StringComparison.OrdinalIgnoreCase));
            spinner.SetSelection(Math.Max(0, index));
            spinner.ItemSelected += (_, args) =>
            {
                set(args.Position == 0 ? "" : options[args.Position]);
                visibleCount = pageSize;
                Refresh();
            };
            return spinner;
        }

        AddFilter("gênero", library.Genres, _inspirationGenre, value => _inspirationGenre = value);
        AddFilter("mecânica", library.Mechanics, _inspirationMechanic, value => _inspirationMechanic = value);
        AddFilter("perspectiva", library.Perspectives, _inspirationPerspective,
            value => _inspirationPerspective = value);
        AddFilter("estilo", library.Styles, _inspirationStyle, value => _inspirationStyle = value);
        favorites.CheckedChange += (_, args) => { _inspirationFavorites = args.IsChecked; visibleCount = pageSize; Refresh(); };
        search.TextChanged += (_, _) => { _inspirationQuery = search.Text ?? ""; visibleCount = pageSize; Refresh(); };
        Refresh();
        root.AddView(BuildLauncherNavigation("explore"));
        SetContentView(root);
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
        var names = new[] { "Sem projeto" }.Concat(_store.List()).ToArray();
        var adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerItem, names);
        adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
        var project = new Spinner(this) { Adapter = adapter };
        int index = Array.IndexOf(names, reference.ProjectName);
        project.SetSelection(Math.Max(0, index));
        form.AddView(project);

        AlertDialog? dialog = null;
        form.AddView(LunetUi.Button(this, "Salvar notas", () =>
        {
            try
            {
                _inspirationLibrary!.SetPersonalDetails(reference.Id, reference.Favorite,
                    notes.Text ?? "", project.SelectedItemPosition == 0 ? "" : names[project.SelectedItemPosition]);
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
                        perspective.Text ?? "", style.Text ?? "", tags, idea.Text ?? "", url.Text ?? "");
                else
                    _inspirationLibrary!.Update(original with
                    {
                        Title = title.Text ?? "", Genre = genre.Text ?? "", Perspective = perspective.Text ?? "",
                        VisualStyle = style.Text ?? "", Mechanics = tags, StudyIdea = idea.Text ?? "",
                        Url = url.Text ?? ""
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
