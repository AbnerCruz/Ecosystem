using Android.App;
using Android.Content;
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
    private const int AssetLibraryExportRequestCode = 4115;
    private string? _pendingLibraryAssetId;
    private int _pendingLibraryAssetVersion;
    private ReusableAssetLibrary? _reusableLibrary;

    private ReusableAssetLibrary AssetLibrary => _reusableLibrary ??= new ReusableAssetLibrary(
        System.IO.Path.Combine(FilesDir!.AbsolutePath, "Library"));

    private void ShowReusableAssetLibrary() => ShowReusableAssetLibrary("");

    private void ShowReusableAssetLibrary(string query)
    {
        SaveCurrent();
        IReadOnlyList<LibraryAssetVersion> entries;
        try { entries = AssetLibrary.Search(query); }
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
        panel.AddView(LunetUi.Button(this, string.IsNullOrWhiteSpace(query) ? "Pesquisar assets" : "Busca: " + query,
            () =>
            {
                var input = LunetUi.Input(this, "Título, autor, licença ou tag", query);
                new AlertDialog.Builder(this)!.SetTitle("Pesquisar na biblioteca")!
                    .SetView(input)!
                    .SetNegativeButton("Cancelar", (_, _) => { })!
                    .SetNeutralButton("Limpar", (_, _) => { dialog?.Dismiss(); ShowReusableAssetLibrary(); })!
                    .SetPositiveButton("Buscar", (_, _) =>
                    {
                        dialog?.Dismiss();
                        ShowReusableAssetLibrary(input.Text ?? "");
                    })!.Show();
            }, ButtonStyle.Secondary), LunetUi.FillWidth(Dp(48)));
        panel.AddView(LunetUi.Button(this, "＋ Publicar asset de um projeto", () =>
        {
            dialog?.Dismiss();
            ChooseAssetToPublish(null);
        }, ButtonStyle.Primary), LunetUi.FillWidth(Dp(52)));

        if (entries.Count == 0)
            panel.AddView(LunetUi.Label(this, string.IsNullOrWhiteSpace(query) ? "Biblioteca vazia. Publique um sprite, tilemap, áudio ou outro asset de um projeto." : "Nenhum asset corresponde à busca. Limpe o filtro para ver todos.",
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
            actions.AddView(LunetUi.Button(this, "Verificar", () =>
            {
                try
                {
                    AssetLibrary.VerifyIntegrity(version.Id, version.Version);
                    Toast.MakeText(this, "Integridade verificada: arquivos desta versão conferem.", ToastLength.Long)?.Show();
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    new AlertDialog.Builder(this)!.SetTitle("Integridade comprometida")!
                        .SetMessage(ex.Message + "\\nA biblioteca não foi alterada.")!
                        .SetPositiveButton("Fechar", (_, _) => { })!.Show();
                }
            }, ButtonStyle.Quiet));
            actions.AddView(LunetUi.Button(this, "Exportar ZIP", () =>
            {
                dialog?.Dismiss();
                RequestLibraryAssetExport(version);
            }, ButtonStyle.Quiet));
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
        new AlertDialog.Builder(this)!.SetTitle("Escolher origem")!
            .SetItems(new[] { "Criações do Studio" }.Concat(names).ToArray(), (_, args) =>
            {
                if (args.Which == 0)
                {
                    var drafts = StandaloneWorkspace.ListFiles().Where(IsStandaloneDraft).ToArray();
                    if (drafts.Length == 0) { Toast.MakeText(this, "Nenhuma criação salva.", ToastLength.Short)?.Show(); return; }
                    new AlertDialog.Builder(this)!.SetTitle("Escolher rascunho")!
                        .SetItems(drafts, (_, e) => ShowAssetPublishForm(null, drafts[e.Which], existingId))!
                        .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
                    return;
                }
                try
                {
                    var project = _store.Open(names[args.Which - 1]);
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

    private void ShowAssetPublishForm(LunetProject? project, string source, string? existingId)
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
        var tags = Field("Tags para pesquisa (separadas por vírgula)", "Ex.: personagem, voo, pixel art", "");
        AlertDialog? dialog = null;
        panel.AddView(LunetUi.Button(this, existingId is null ? "Publicar na biblioteca" : "Publicar nova versão", () =>
        {
            try
            {
                var parsedTags = (tags.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);
                if (project is null)
                {
                    var rendered = PrepareLibraryRender(source, StandaloneWorkspace);
                    AssetLibrary.PublishWorkspace(StandaloneWorkspace.RootDirectory, source, title.Text ?? "",
                        author.Text ?? "", license.Text ?? "", attribution.Text ?? "", parsedTags, existingId, rendered);
                }
                else AssetLibrary.Publish(project, source, title.Text ?? "", author.Text ?? "",
                    license.Text ?? "", attribution.Text ?? "", tags: parsedTags, existingAssetId: existingId,
                    render: PrepareLibraryRender(source, new StudioAssetWorkspace(project.Directory)));
                dialog?.Dismiss();
                ShowReusableAssetLibrary();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or ProjectException or UnauthorizedAccessException or InvalidOperationException)
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


    private void RequestLibraryAssetExport(LibraryAssetVersion version)
    {
        _pendingLibraryAssetId = version.Id;
        _pendingLibraryAssetVersion = version.Version;
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/zip");
        intent.PutExtra(Intent.ExtraTitle, "lunet-asset-" + version.Id + "-v" + version.Version + ".zip");
        try
        {
            StartActivityForResult(intent, AssetLibraryExportRequestCode);
        }
        catch (global::Android.Content.ActivityNotFoundException)
        {
            _pendingLibraryAssetId = null;
            _pendingLibraryAssetVersion = 0;
            new AlertDialog.Builder(this)!.SetTitle("Exportação indisponível")!
                .SetMessage("Nenhum seletor de documentos compatível foi encontrado neste aparelho. O asset permanece na biblioteca.")!
                .SetPositiveButton("Fechar", (_, _) => { })!.Show();
        }
        catch (Java.Lang.SecurityException)
        {
            _pendingLibraryAssetId = null;
            _pendingLibraryAssetVersion = 0;
            new AlertDialog.Builder(this)!.SetTitle("Exportação bloqueada")!
                .SetMessage("O sistema impediu a abertura do seletor de documentos. O asset permanece na biblioteca.")!
                .SetPositiveButton("Fechar", (_, _) => { })!.Show();
        }
    }

    private void CompleteLibraryAssetExport(Result resultCode, global::Android.Net.Uri? uri)
    {
        var assetId = _pendingLibraryAssetId;
        var version = _pendingLibraryAssetVersion;
        _pendingLibraryAssetId = null;
        _pendingLibraryAssetVersion = 0;
        if (resultCode != Result.Ok || uri is null || assetId is null) return;

        // ExportZip validates the complete immutable version before touching the SAF destination.
        // SAF may overwrite an existing document selected by the user, so do not open its stream
        // until the archive has been assembled and verified locally.
        var temp = System.IO.Path.Combine(CacheDir!.AbsolutePath, Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            AssetLibrary.ExportZip(assetId, version, temp);
            using var source = System.IO.File.OpenRead(temp);
            using var target = ContentResolver?.OpenOutputStream(uri) ??
                throw new IOException("Não foi possível abrir o destino.");
            source.CopyTo(target);
            target.Flush();
            Toast.MakeText(this, "Versão exportada em ZIP", ToastLength.Short)?.Show();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            new AlertDialog.Builder(this)!.SetTitle("Exportação não concluída")!
                .SetMessage(ex.Message + "\nVerifique o destino antes de reutilizar o arquivo.")!
                .SetPositiveButton("Fechar", (_, _) => { })!.Show();
        }
        finally
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
            catch (IOException) { /* A falha na limpeza não deve mascarar o erro original. */ }
        }
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
                try
                {
                    var destination = _store.Open(selectedName);
                    var conflicts = AssetLibrary.FindImportConflicts(version.Id, version.Version, destination);
                    if (conflicts.Count > 0)
                    {
                        new AlertDialog.Builder(this)!.SetTitle("Conflito de arquivos")!
                            .SetMessage("A importação foi bloqueada para preservar o projeto. Já existem:\n" +
                                string.Join("\n", conflicts) + "\nRenomeie ou mova os arquivos antes de tentar novamente.")!
                            .SetPositiveButton("Fechar", (_, _) => { })!.Show();
                        return;
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or ProjectException or UnauthorizedAccessException or InvalidOperationException)
                {
                    Toast.MakeText(this, "Não foi possível verificar o destino: " + ex.Message, ToastLength.Long)?.Show();
                    return;
                }
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
                        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or ProjectException or UnauthorizedAccessException or InvalidOperationException)
                        {
                            new AlertDialog.Builder(this)!.SetTitle("Importação não realizada")!
                                .SetMessage(ex.Message + "\nO Lunet não sobrescreveu arquivos existentes.")!
                                .SetPositiveButton("Fechar", (_, _) => { })!.Show();
                        }
                    })!.Show();
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }
}
