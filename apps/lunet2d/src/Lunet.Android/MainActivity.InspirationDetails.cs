using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private void ShowInspirationDetail(InspirationReference reference, Action refresh)
    {
        var body = LunetUi.Column(this, 10);
        body.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
        body.AddView(LunetUi.Label(this, reference.Curated ? "Ficha do catálogo Lunet" : "Referência pessoal",
            LunetUi.TextCaption, LunetUi.TextMuted));
        void Section(string title, string value)
        {
            body.AddView(LunetUi.Label(this, title, LunetUi.TextLabel, LunetUi.AccentText, bold: true));
            body.AddView(LunetUi.Label(this, value, LunetUi.TextBody, LunetUi.Text));
        }
        Section("Mecânicas registradas", string.Join(" · ", reference.Mechanics));
        Section("O que observar", reference.StudyIdea);
        Section("Autoria e direitos", reference.Curated
            ? "Texto de curadoria Lunet; jogo e mídia pertencem aos respectivos titulares. Nenhuma mídia comercial distribuída."
            : (string.IsNullOrWhiteSpace(reference.Author) ? "Autoria não informada" : reference.Author) + " · " +
              (string.IsNullOrWhiteSpace(reference.License) ? "Licença não informada; não implica autorização de uso." : reference.License));
        Section("Suas observações", string.IsNullOrWhiteSpace(reference.Notes) ? "Nenhuma observação ainda." : reference.Notes);
        body.AddView(LunetUi.Label(this, "Experimento sugerido (não é descrição do jogo)", LunetUi.TextLabel, LunetUi.AccentText, bold: true));
        var adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerItem, reference.Mechanics);
        adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
        var mechanic = new Spinner(this) { Adapter = adapter };
        var idea = LunetUi.Label(this, InspirationLibrary.SuggestExperiment(reference), LunetUi.TextBody, LunetUi.Text);
        mechanic.ItemSelected += (_, args) => idea.Text = InspirationLibrary.SuggestExperiment(reference, reference.Mechanics[args.Position]);
        body.AddView(mechanic);
        body.AddView(idea);
        var scroll = new ScrollView(this);
        scroll.AddView(body);
        AlertDialog? dialog = null;
        body.AddView(LunetUi.Button(this, "Anotar experimento", () =>
        {
            try
            {
                var library = _inspirationLibrary!;
                var current = library.All.Single(e => e.Id == reference.Id);
                var notes = string.Join("\n\n", new[] { current.Notes, "Experimento proposto: " + (idea.Text ?? "") }
                    .Where(n => !string.IsNullOrWhiteSpace(n)));
                library.SetPersonalDetails(current.Id, current.Favorite, notes, current.ProjectName);
                dialog?.Dismiss();
                refresh();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            { Toast.MakeText(this, "Não foi possível anotar: " + ex.Message, ToastLength.Long)?.Show(); }
        }, ButtonStyle.Primary));
        body.AddView(LunetUi.Button(this, "Notas e projeto", () =>
        {
            dialog?.Dismiss();
            EditInspirationNotes(reference, refresh);
        }, ButtonStyle.Secondary));
        body.AddView(LunetUi.Button(this, "Documentação offline", () =>
        {
            dialog?.Dismiss();
            var position = mechanic.SelectedItemPosition;
            var selected = position >= 0 && position < reference.Mechanics.Length ? reference.Mechanics[position] : null;
            if (!DocumentationPanel.Open(this, "guide:" + InspirationLibrary.GuideFor(selected))) ShowReference();
        }, ButtonStyle.Quiet));
        dialog = new AlertDialog.Builder(this)!.SetTitle(reference.Title)!.SetView(scroll)!
            .SetNegativeButton("Voltar", (_, _) => { })!.Create();
        dialog.Show();
    }

    private void ShowInspirationComparison()
    {
        var selection = _inspirationComparison.ToArray();
        if (selection.Length != 2) return;
        var comparison = _inspirationLibrary!.Compare(selection[0], selection[1]);
        var body = LunetUi.Column(this, 12);
        body.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
        static string Tags(string[] values) => values.Length == 0 ? "Nenhuma" : string.Join(" · ", values);
        body.AddView(LunetUi.Label(this, "Mecânicas em comum", LunetUi.TextLabel, LunetUi.AccentText, bold: true));
        body.AddView(LunetUi.Label(this, Tags(comparison.SharedMechanics), LunetUi.TextBody, LunetUi.Text));
        foreach (var item in new[] { (comparison.First, comparison.FirstOnly), (comparison.Second, comparison.SecondOnly) })
        {
            var card = LunetUi.Column(this, 5);
            card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            card.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
            card.AddView(LunetUi.Label(this, item.Item1.Title, 16, LunetUi.Text, bold: true));
            card.AddView(LunetUi.Label(this, item.Item1.Genre + " · " + item.Item1.Perspective + " · " + item.Item1.VisualStyle,
                LunetUi.TextCaption, LunetUi.TextMuted));
            card.AddView(LunetUi.Label(this, "Mecânicas exclusivas nesta comparação: " + Tags(item.Item2), LunetUi.TextBody, LunetUi.Text));
            card.AddView(LunetUi.Label(this, "O que observar: " + item.Item1.StudyIdea, LunetUi.TextBody, LunetUi.Text));
            card.AddView(LunetUi.Label(this, "Experimento sugerido: " + InspirationLibrary.SuggestExperiment(item.Item1,
                comparison.SharedMechanics.FirstOrDefault()), LunetUi.TextBody, LunetUi.Text));
            body.AddView(card);
        }
        body.AddView(LunetUi.Label(this, "Compare decisões e resultados no seu próprio protótipo, sem copiar assets ou código dos jogos.",
            LunetUi.TextCaption, LunetUi.TextMuted));
        var scroll = new ScrollView(this);
        scroll.AddView(body);
        new AlertDialog.Builder(this)!.SetTitle("Comparar referências")!.SetView(scroll)!
            .SetPositiveButton("Voltar", (_, _) => { })!.Show();
    }

    private void ShowArchivedInspiration(Action refresh)
    {
        var library = _inspirationLibrary!;
        var entries = library.Archived;
        if (entries.Count == 0)
        {
            new AlertDialog.Builder(this)!.SetTitle("Referências arquivadas")!.SetMessage("Nenhuma referência arquivada.")!
                .SetPositiveButton("Voltar", (_, _) => { })!.Show();
            return;
        }
        var items = entries.Select(e => e.Title + (string.IsNullOrEmpty(e.ProjectName) ? "" : " · " + e.ProjectName)).ToArray();
        new AlertDialog.Builder(this)!.SetTitle("Restaurar referência arquivada")!.SetItems(items, (_, args) =>
        {
            var entry = entries[args.Which];
            try { library.Restore(entry.Id); refresh(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
        })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }
}
