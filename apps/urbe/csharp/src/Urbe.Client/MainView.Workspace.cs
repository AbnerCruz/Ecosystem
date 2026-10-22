using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The workspace maintenance commands of the 1.8.4: "Limpar referências órfãs" (ui/vault-notices.js
/// workspace.cleanOrphans over persistence/gc.js: shows what it found and only deletes after a confirmation, then
/// records it in .urbe/vault.json) and "Diagnóstico do workspace" (core/diagnostics.js).
/// </summary>
public sealed partial class MainView
{
    /// <summary>gc.js orphanDays: a history only leaves after 30 days without change.</summary>
    private const int OrphanDays = 30;

    /// <summary>workspace.cleanOrphans: the histories of notes that no longer exist (not alive, not in the trash).</summary>
    public async Task CleanOrphansAsync()
    {
        if (_vault is null || !VaultWritable || _store is null)
        {
            await Dialogs.AlertAsync("Limpar referências órfãs", NoVaultMessage);
            return;
        }
        var now = DateTimeOffset.UtcNow;
        var known = new HashSet<string>(_store.List().Select(d => d.Id), StringComparer.Ordinal);
        foreach (var item in _trash.Items)
            if (item["document"]?["id"] is JsonValue v && v.TryGetValue<string>(out var id)) known.Add(id);
        var orphans = _historyReadOnly ? [] : _history.Orphans(known.Contains, now.ToUnixTimeMilliseconds(), OrphanDays);
        if (orphans.Count == 0)
        {
            ShowLifeNotice("Nada para limpar: não há referências a notas que não existem mais.");   // informs only: a toast (UC-69)
            return;
        }
        var found = orphans.Count == 1 ? "1 histórico de uma nota que não existe mais há mais de 30 dias"
            : orphans.Count + " históricos de notas que não existem mais há mais de 30 dias";
        if (!await Dialogs.ConfirmAsync("Limpar referências órfãs?", "Encontrei " + found + ". Notas, notas na lixeira e o histórico delas não são tocados.", "Limpar"))
            return;
        foreach (var orphan in orphans) _history.Forget(orphan.Id);
        try
        {
            await _vault.SaveHistoryAsync(_history.Export());
            _historyDirty = false;
            await _vault.SaveMaintenanceAsync(new JsonObject
            {
                ["kind"] = "gc",
                ["at"] = now.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                ["removed"] = new JsonObject { ["history"] = orphans.Count, ["trash"] = 0, ["compositionRefs"] = 0 },
                ["orphanDays"] = OrphanDays,
                ["trashDays"] = null
            });
        }
        catch (Exception ex)
        {
            _historyDirty = true;
            await Dialogs.AlertAsync("Não foi possível limpar", ex.Message);
            return;
        }
        ShowLifeNotice("Limpeza feita e registrada no vault.", Ui.ToastKind.Success);
    }

    /// <summary>The version of this app, for the diagnosis.</summary>
    private static string AppVersion =>
        typeof(MainView).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(MainView).Assembly.GetName().Version?.ToString() ?? "";

    private DateTimeOffset? _lastSavedAt;

    /// <summary>workspace.diagnostics: what is wrong, then the counts and the state of the saves.</summary>
    public async Task ShowDiagnosticsAsync()
    {
        var issues = new List<string>();
        if (_vault is null || !_vault.IsConnected) issues.Add("Persistência indisponível");
        if (_notesSave?.State is "error" or "refused" && _notesSave.Problem is { Length: > 0 } problem) issues.Add("Registro das notas: " + problem);
        int documents = _store?.List().Count ?? 0;
        int links = 0;
        if (_store is not null)
        {
            using var index = new KnowledgeIndex(_store);
            links = index.Stats().Links;
        }
        var state = _notesSave?.State switch
        {
            "saved" => "salvo", "saving" => "salvando", "dirty" => "aguardando", "error" => "erro", "refused" => "recusado", "idle" => "parado",
            null => null, var other => other
        };
        var message = (issues.Count == 0 ? "Nenhum problema encontrado." : "Problemas:\n- " + string.Join("\n- ", issues)) +
            "\n\nNotas e arquivos: " + documents +
            "\nPastas: " + (_library?.Folders.Count ?? 0) +
            "\nLigações: " + links +
            (state is not null ? "\nGravação: " + state + (_lastSavedAt is { } at ? " (" + at.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ")" : "") : "") +
            "\nVersão: " + AppVersion;
        await Dialogs.AlertAsync(issues.Count == 0 ? "Tudo certo" : "Diagnóstico", message);
    }
}
