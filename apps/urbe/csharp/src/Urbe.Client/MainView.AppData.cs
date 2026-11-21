using System.Globalization;
using Urbe.Core;
using Urbe.Core.Study;

namespace Urbe.Client;

/// <summary>
/// #1005 (proposal P5 of DATA-INVENTORY-20261120): what the app keeps and where, shown read-only — the vault's hidden
/// .urbe files, what stays only on this device, and the folders the app itself made in the Explorer — with sizes and
/// what each is for. Nothing is moved or deleted here.
/// </summary>
public sealed partial class MainView
{
    /// <summary>One thing the app keeps: where, what it is, its size in bytes (null when unknown) and how many files.</summary>
    public sealed record AppDataItem(string Group, string Name, string Where, string Purpose, long? Bytes, int Files);

    private static readonly (string Path, string Name, string Purpose)[] VaultRecords =
    [
        (NoteLibrary.Path, "Registro das notas", "Ids, datas e pastas de cada nota."),
        (FlashcardLibrary.Path, "Flashcards", "Os conjuntos de cartões feitos no app."),
        (LegacyTrash.Path, "Lixeira", "O que foi para a lixeira, para restaurar."),
        (LegacyHistory.Path, "Versões anteriores", "O histórico das notas."),
        (AreaLayout.Path, "Área de trabalho", "Cor, ordem e modo de cada caixa."),
        (Constellation.Path, "Constelação", "Posições do céu de notas (refeitas quando faltam)."),
        (VaultIdentity.Path, "Identidade do vault", "Distingue este vault dos outros."),
        (VaultCompatibility.Path, "Compatibilidade", "Com qual versão o vault foi aberto."),
        (".urbe/mapa.json", "Mapa da 1.8.4", "Guardado como estava; o app não o reescreve."),
    ];

    private static readonly (string Folder, string Purpose)[] AppFolders =
    [
        ("Personalização", "Tema, editor, atalhos, plugins e skills (editáveis à mão)."),
        ("Tutorial", "Cópia do guia do app."),
        ("Publicações", "Projetos de publicação e as falas das cenas de vídeo."),
        ("Narração", "Áudios de narração gerados (refeitos a partir das notas)."),
        ("Vídeos", "MP4 gerados."),
        ("Imagens", "Imagens geradas pelo Assistente."),
        ("Estudos", "Sessões de estudo."),
        ("Notas rápidas", "Notas rápidas, por mês."),
    ];

    /// <summary>The items, measured now.</summary>
    public async Task<IReadOnlyList<AppDataItem>> AppDataAsync()
    {
        var items = new List<AppDataItem>();
        if (_vault is { } vault)
            foreach (var (path, name, purpose) in VaultRecords)
            {
                long? size = null;
                try { size = (await vault.ReadFileAsync(path, 0))?.Size; }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { }
                if (size is not null) items.Add(new AppDataItem("No vault, oculto do Explorer", name, path, purpose, size, 1));
            }
        if (UrbeApp.LocalStatePath is { } state && Path.GetDirectoryName(state) is { } dir && Directory.Exists(dir))
        {
            void Local(string name, string path, string purpose)
            {
                if (File.Exists(path)) items.Add(new AppDataItem("Só neste aparelho", name, path, purpose, new FileInfo(path).Length, 1));
                else if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    items.Add(new AppDataItem("Só neste aparelho", name, path, purpose, files.Sum(f => new FileInfo(f).Length), files.Length));
                }
            }
            Local("Progresso dos flashcards", state + ".study.v1.json", "Quando cada cartão volta (revisão espaçada).");
            Local("Posições de leitura", state + ".readers.v1.json", "Onde você parou em cada PDF, livro ou vídeo.");
            Local("Voz da narração", state + ".voice.txt", "A voz escolhida para narrar.");
            Local("Rascunhos não salvos", Path.Combine(dir, "rascunhos"), "Texto digitado antes de o app fechar, para recuperar.");
        }
        if (UrbeApp.AssistantFolder is { } assistant && Directory.Exists(assistant))
        {
            var files = Directory.GetFiles(assistant, "*", SearchOption.AllDirectories);
            items.Add(new AppDataItem("Só neste aparelho", "Conversas e memória do Assistente", assistant, "Conversas, anexos e memória dos agentes.", files.Sum(f => new FileInfo(f).Length), files.Length));
        }
        var all = (_store?.List().Select(d => d.Path) ?? []).Concat(_attachments).ToList();
        foreach (var (folder, purpose) in AppFolders)
        {
            int count = all.Count(p => p.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
            if (count > 0) items.Add(new AppDataItem("No Explorer, feito pelo app", folder, folder + "/", purpose, null, count));
        }
        return items;
    }

    public static string Size(long? bytes) => bytes switch
    {
        null => "",
        < 1024 => bytes + " B",
        < 1024 * 1024 => (bytes.Value / 1024.0).ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")) + " KB",
        _ => (bytes.Value / 1024.0 / 1024).ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")) + " MB"
    };

    /// <summary>The list, grouped, read-only: choosing an item says where it is and what it is for.</summary>
    public async Task ShowAppDataAsync()
    {
        var items = await AppDataAsync();
        if (items.Count == 0) { await Dialogs.AlertAsync("Dados do aplicativo", "Nada guardado ainda."); return; }
        var rows = items.Select(i => new DialogItem(i.Name, i.Group.StartsWith("No vault", StringComparison.Ordinal) ? UrbeTheme.Icons.Storage : i.Group.StartsWith("Só", StringComparison.Ordinal) ? UrbeTheme.Icons.Clock : UrbeTheme.Icons.Folder,
            i.Group + " · " + (i.Files > 1 ? i.Files + " arquivos" + (i.Bytes is not null ? ", " : "") : "") + Size(i.Bytes))).ToList();
        while (await Dialogs.ChooseAsync("Dados do aplicativo", rows, "O que o Numa guarda e onde. Nada aqui é apagado ou movido.") is int pick)
        {
            var it = items[pick];
            await Dialogs.AlertAsync(it.Name, it.Purpose + "\n\n" + it.Group + ": " + it.Where +
                (it.Group == "Só neste aparelho" ? "\n\nFica fora da pasta de notas: não vai junto em cópias da pasta e some se o app for desinstalado." : ""));
        }
    }
}
