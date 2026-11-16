using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// Biblioteca pessoal fora dos projetos, com versões imutáveis e importação por cópia.
/// Os Studios permanecem os editores reais do projeto; sua sessão global será desacoplada separadamente.
/// </summary>
public sealed partial class MainActivity
{
    private ReusableAssetLibrary? _reusableLibrary;

    private ReusableAssetLibrary AssetLibrary => _reusableLibrary ??= new ReusableAssetLibrary(
        System.IO.Path.Combine(FilesDir!.AbsolutePath, "Library"));

    private void ShowReusableAssetLibrary()
    {
        SaveCurrent();
        IReadOnlyList<LibraryAssetVersion> entries;
        try { entries = AssetLibrary.List(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            new AlertDialog.Builder(this)!.SetTitle("Biblioteca indisponível")!
                .SetMessage(ex.Message + "\nOs arquivos existentes não foram apagados.")!
                .SetPositiveButton("Fechar", (_, _) => { })!.Show();
            return;
        }

        var panel = LunetUi.Column(this, 10);
        panel.SetPadding(Dp(14), Dp(10), Dp(14), Dp(12));
        panel.AddView(LunetUi.Label(this,
            "Assets pessoais, reutilizáveis entre jogos. Cada importação copia os arquivos e preserva a versão usada.",
            LunetUi.TextBody, LunetUi.TextMuted));
        AlertDialog? dialog = null;
        panel.AddView(LunetUi.Button(this, "＋ Publicar asset de um projeto", () =>
        {
            dialog?.Dismiss();
            ChooseAssetToPublish(null);
        }, ButtonStyle.Primary), LunetUi.FillWidth(Dp(52)));

        if (entries.Count == 0)
            panel.AddView(LunetUi.Label(this, "Biblioteca vazia. Publique um sprite, tilemap, áudio ou outro asset de um projeto.",
                LunetUi.TextBody, LunetUi.TextMuted));

        foreach (var asset in entries)
        {
            var version = asset;
            var card = LunetUi.Column(this, 7);
            card.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
            card.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
            card.AddView(LunetUi.Label(this, version.Title + " · v" + version.Version,
                LunetUi.TextBody, LunetUi.Text, bold: true));
            card.AddView(LunetUi.Label(this,
                version.Kind + " · " + version.Files.Length + " arquivo(s) · licença: " + version.License,
                LunetUi.TextLabel, LunetUi.TextMuted));
            if (!string.IsNullOrWhiteSpace(version.Author))
                card.AddView(LunetUi.Label(this, "Autor: " + version.Author,
                    LunetUi.TextCaption, LunetUi.TextMuted));
            if (!string.IsNullOrWhiteSpace(version.Attribution))
                card.AddView(LunetUi.Label(this, "Atribuição: " + version.Attribution,
                    LunetUi.TextCaption, LunetUi.TextMuted));
            var actions = LunetUi.Row(this, 8);
            actions.AddView(LunetUi.Button(this, "Usar em…", () =>
            {
                dialog?.Dismiss();
                ChooseAssetDestination(version);
            }, ButtonStyle.Secondary));
            actions.AddView(LunetUi.Button(this, "Nova versão", () =>
            {
                dialog?.Dismiss();
                ChooseAssetToPublish(version.Id);
            }, ButtonStyle.Quiet));
            card.AddView(actions);
            panel.AddView(card, LunetUi.FillWidth());
        }

        var scroll = new ScrollView(this);
        scroll.AddView(panel);
        dialog = new AlertDialog.Builder(this)!.SetTitle("Biblioteca de assets")!
            .SetView(scroll)!.SetNegativeButton("Fechar", (_, _) => { })!.Create();
        dialog.Show();
    }

    private void ChooseAssetToPublish(string? existingId)
    {
        var names = _store.List().ToArray();
        if (names.Length == 0)
        {
            new AlertDialog.Builder(this)!.SetTitle("Nenhum projeto")!
                .SetMessage("Crie um jogo e produza um asset antes de publicá-lo na biblioteca.")!
                .SetPositiveButton("Fechar", (_, _) => { })!.Show();
            return;
        }
        new AlertDialog.Builder(this)!.SetTitle("Escolher projeto de origem")!
            .SetItems(names, (_, args) =>
            {
                try
                {
                    var project = _store.Open(names[args.Which]);
                    var files = ProjectAssetCatalog.Build(project.ListFiles());
                    var choices = files.Select(a => a.Path).ToArray();
                    if (choices.Length == 0)
                    {
                        Toast.MakeText(this, "Este projeto ainda não tem assets publicáveis.", ToastLength.Long)?.Show();
                        return;
                    }
                    new AlertDialog.Builder(this)!.SetTitle("Escolher asset")!
                        .SetItems(choices, (_, selected) => ShowAssetPublishForm(project, choices[selected.Which], existingId))!
                        .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
                }
                catch (Exception ex) when (ex is IOException or ProjectException or InvalidDataException)
                { Toast.MakeText(this, "Não foi possível listar assets: " + ex.Message, ToastLength.Long)?.Show(); }
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void ShowAssetPublishForm(LunetProject project, string source, string? existingId)
    {
        var panel = LunetUi.Column(this, 8);
        panel.SetPadding(Dp(16), Dp(10), Dp(16), Dp(10));
        EditText Field(string caption, string hint, string value)
        {
            panel.AddView(LunetUi.Label(this, caption, LunetUi.TextLabel, LunetUi.TextMuted));
            var input = LunetUi.Input(this, hint, value);
            panel.AddView(input, LunetUi.FillWidth(Dp(48)));
            return input;
        }
        panel.AddView(LunetUi.Label(this, source, LunetUi.TextCaption, LunetUi.AccentText));
        var title = Field("Nome do asset", "Ex.: Dragão voador", System.IO.Path.GetFileNameWithoutExtension(source));
        var author = Field("Autor", "Nome ou autoria própria", "");
        var license = Field("Licença", "Ex.: CC0, CC-BY ou uso privado", "Uso privado");
        var attribution = Field("Atribuição / origem", "Fonte original e condições de uso", "");
        AlertDialog? dialog = null;
        panel.AddView(LunetUi.Button(this, existingId is null ? "Publicar na biblioteca" : "Publicar nova versão", () =>
        {
            try
            {
                AssetLibrary.Publish(project, source, title.Text ?? "", author.Text ?? "",
                    license.Text ?? "", attribution.Text ?? "", existingAssetId: existingId);
                dialog?.Dismiss();
                ShowReusableAssetLibrary();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or ProjectException or UnauthorizedAccessException)
            {
                Toast.MakeText(this, "Não foi possível publicar: " + ex.Message, ToastLength.Long)?.Show();
            }
        }, ButtonStyle.Primary), LunetUi.FillWidth(Dp(52)));
        var scroll = new ScrollView(this);
        scroll.AddView(panel);
        dialog = new AlertDialog.Builder(this)!.SetTitle(existingId is null ? "Publicar asset" : "Nova versão")!
            .SetView(scroll)!.SetNegativeButton("Cancelar", (_, _) => { })!.Create();
        dialog.Show();
    }

    private void ChooseAssetDestination(LibraryAssetVersion version)
    {
        var names = _store.List().ToArray();
        if (names.Length == 0)
        {
            Toast.MakeText(this, "Crie um projeto antes de importar assets.", ToastLength.Long)?.Show();
            return;
        }
        new AlertDialog.Builder(this)!.SetTitle("Usar " + version.Title + " em qual projeto?")!
            .SetItems(names, (_, args) =>
            {
                var selectedName = names[args.Which];
                new AlertDialog.Builder(this)!
                    .SetTitle("Importar cópia imutável?")!
                    .SetMessage("Os arquivos serão copiados para " + selectedName + ":\n" +
                        string.Join("\n", version.Files.Select(f => f.Path)) +
                        "\nArquivos com o mesmo caminho nunca serão sobrescritos.")!
                    .SetNegativeButton("Cancelar", (_, _) => { })!
                    .SetPositiveButton("Importar cópia", (_, _) =>
                    {
                        try
                        {
                            var project = _store.Open(selectedName);
                            AssetLibrary.ImportToProject(version.Id, version.Version, project);
                            new AlertDialog.Builder(this)!.SetTitle("Asset importado")!
                                .SetMessage("Os arquivos agora pertencem ao projeto " + selectedName +
                                    ". Revisões futuras da biblioteca não alteram este jogo.")!
                                .SetNegativeButton("Fechar", (_, _) => { })!
                                .SetPositiveButton("Abrir projeto", (_, _) => OpenProject(selectedName))!.Show();
                        }
                        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or ProjectException or UnauthorizedAccessException)
                        {
                            new AlertDialog.Builder(this)!.SetTitle("Importação não realizada")!
                                .SetMessage(ex.Message + "\nO Lunet não sobrescreveu arquivos existentes.")!
                                .SetPositiveButton("Fechar", (_, _) => { })!.Show();
                        }
                    })!.Show();
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }
}
