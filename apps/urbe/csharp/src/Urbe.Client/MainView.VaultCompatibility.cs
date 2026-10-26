using Urbe.Core;

namespace Urbe.Client;

public sealed partial class MainView
{
    /// <summary>Explain per-file incompatibilities without hiding the rest of the vault.</summary>
    public async Task ShowVaultCompatibilityAsync()
    {
        if (_snapshot is not { } snapshot) return;
        var assessment = VaultCompatibility.Assess(snapshot.Files, InstalledVersion);
        var protectedPaths = snapshot.FutureFiles.Where(p => !p.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        if (snapshot.IsReadOnly)
        {
            await Dialogs.AlertAsync("Compatibilidade do vault",
                assessment.ManifestUnsafe
                    ? "O manifesto de compatibilidade está ilegível ou é de uma versão desconhecida. Nenhum arquivo será regravado. Preserve o vault e abra uma versão que compreenda o manifesto."
                    : "Este vault usa metadados de uma versão futura e permanece em modo somente leitura. Preserve um backup antes de tentar qualquer migração.");
            return;
        }
        if (protectedPaths.Length == 0)
        {
            await Dialogs.AlertAsync("Compatibilidade do vault",
                "Nenhum arquivo incompatível foi identificado. Isso não garante que versões antigas compreendam recursos que não declaram requisitos de compatibilidade.");
            return;
        }

        var chosen = await Dialogs.ChooseAsync("Arquivos protegidos",
            protectedPaths.Select(path => new DialogItem(path, Detail:
                assessment.BlockedFiles.GetValueOrDefault(path) ?? "Formato de arquivo de uma versão futura.")).ToArray(),
            protectedPaths.Length + " arquivo(s) protegidos contra gravação nesta versão. Os demais podem continuar sendo editados.");
        if (chosen is null) return;
        var target = protectedPaths[chosen.Value];
        var isNote = _store?.Get(target) is not null;
        var operation = await Dialogs.ChooseAsync("Arquivo protegido — " + target,
        [
            new DialogItem("Preservar original", Detail: "Não modificar o arquivo protegido."),
            new DialogItem("Criar cópia independente", Detail: isNote
                ? "Duplicar o arquivo no vault. O original permanece intacto; a cópia pode exigir revisão manual antes de ser editável."
                : "Disponível para documentos de texto reconhecidos pelo Urbe."),
            new DialogItem("Mover original para a lixeira", Detail: isNote
                ? "Exclusão deliberada: pedir confirmação e preservar o conteúdo na lixeira."
                : "Disponível somente para notas reconhecidas pelo Urbe.")
        ], "Alterar a versão instalada não converte automaticamente o conteúdo deste arquivo.");
        if (operation is null || operation == 0) return;
        if (!isNote)
        {
            await Dialogs.AlertAsync("Arquivo protegido",
                "Este formato não pode ser copiado ou removido pela rotina de notas. Faça uma cópia de segurança do vault e use uma versão capaz de interpretar esse arquivo.");
            return;
        }
        if (operation == 1)
        {
            await DuplicateNotesAsync([target]);
            ShowLifeNotice("Cópia criada. O original permanece protegido. Revise a compatibilidade do conteúdo copiado.");
        }
        else if (operation == 2)
        {
            if (await Dialogs.ConfirmAsync("Mover arquivo incompatível para a lixeira?",
                    "O conteúdo original será mantido na lixeira; a proteção de versão não será ignorada silenciosamente. Arquivo: " + target,
                    "Mover para a lixeira", danger: true))
                await DeleteNoteAsync(target, confirmed: true);
        }
    }
}
