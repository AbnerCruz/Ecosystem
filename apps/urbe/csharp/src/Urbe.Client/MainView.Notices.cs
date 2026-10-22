using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// ui/vault-notices.js (what the opened vault means for the person: a newer format opened read-only, files of a newer
/// version kept untouched) and ui/tips.js (first-use tips, each shown once, "Entendi" kept on this device).
/// </summary>
public sealed partial class MainView
{
    /// <summary>workspace:readonly / workspace:foreign, after a vault opens.</summary>
    private void VaultNotices(VaultSnapshot snapshot)
    {
        if (_vault is null) return;
        if (_identityNotice is { } recognized) ShowLifeNotice(recognized);
        if (snapshot.IsReadOnly && snapshot.FormatState == VaultFormatState.Future)
        {
            var msg = "Este vault foi criado por uma versão mais nova do Urbe (formato " + snapshot.VaultFormatVersion + "). Para não corromper nada, ele está aberto somente para leitura. Atualize o Urbe para editar.";
            Dispatcher.UIThread.Post(() => _ = Dialogs.AlertAsync("Vault somente leitura", msg), DispatcherPriority.Background);
            return;
        }
        var foreign = snapshot.FutureFiles.Where(p => p != ".urbe/vault.json").ToList();
        if (foreign.Count > 0)
            ShowLifeNotice("Arquivos de uma versão mais nova do Urbe foram preservados sem alteração: " + string.Join(", ", foreign.Select(p => p.StartsWith(".urbe/", StringComparison.Ordinal) ? p[6..] : p)) + ".");
    }

    // ---------------------------------------------------------------- identity.js (REQ-042)
    // read at the opening (OpeningIds): a note renamed or moved outside the app while it was closed keeps its id

    private VaultIdentityState? _identity;
    private bool _identityReadOnly;
    private byte[]? _identityOnDisk;
    private string? _identityNotice;

    /// <summary>workspace.js flush: the sidecar rebuilt from the notes (seen kept for who did not change), written only when it differs.</summary>
    private async Task SaveIdentityAsync()
    {
        if (_vault is null || _identityReadOnly || _store is null || !VaultWritable) return;
        var built = VaultIdentity.Build(_store.List().Select(d => new VaultWriteDocument(d.Id, d.Path, d.Content)), _identity, DateTimeOffset.UtcNow);
        var bytes = VaultIdentity.Serialize(built);
        if (_identityOnDisk is { } current && current.AsSpan().SequenceEqual(bytes)) return;
        // the same documents with the same seen: nothing to write (the file is not rewritten for nothing)
        if (_identity is { } previous && previous.Documents.Count == built.Documents.Count &&
            built.Documents.All(p => previous.Documents.TryGetValue(p.Key, out var o) && o == p.Value)) return;
        try
        {
            await _vault.SaveIdentityAsync(bytes);
            _identity = built;
            _identityOnDisk = bytes;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
        {
            // derived record: the notes and their record are already saved; the next save tries again
        }
    }

    // ---------------------------------------------------------------- tips.js

    private Border? _tip;

    private static bool TipSeen(Customization c, string k) => c.Device.Get("urbe.tip." + k) == "1";
    private static void TipMark(Customization c, string k) => c.Device.Set("urbe.tip." + k, "1");

    /// <summary>The tip shown now (tests): its key, or null.</summary>
    public string? TipShown { get; private set; }

    private int LinkCount()
    {
        if (_store is null) return 0;
        using var index = new KnowledgeIndex(_store);
        return index.Stats().Links;
    }

    private void WatchTips()
    {
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty) return;
            if (Editor.IsVisible) HideTip();
            else DispatcherTimer.RunOnce(MaybeLinkTip, TimeSpan.FromMilliseconds(300));
        };
        NoteSaved += _ => { if (TipShown == "link" && LinkCount() > 0) { TipMark(Customization, "link"); HideTip(); } };
    }

    /// <summary>workspace:loaded: the link tip, a moment after the notes show.</summary>
    private void TipsLoaded() => DispatcherTimer.RunOnce(MaybeLinkTip, TimeSpan.FromMilliseconds(300));

    /// <summary>Back in the Explorer with notes but no link yet: "Ligue suas notas".</summary>
    public void MaybeLinkTip()
    {
        if (Editor.IsVisible || _store is null || _store.List().Count < 1) return;
        if (TipSeen(Customization, "link") || LinkCount() > 0) return;
        ShowTip("link", "Ligue suas notas", "Dentro de uma nota, digite [[ e escolha outra nota. As duas ficam ligadas, e cada uma mostra a outra em Estrutura e backlinks.");
    }

    private void ShowTip(string key, string title, string text)
    {
        if (TipShown is not null) return;
        var ok = new Button
        {
            Content = "Entendi", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Height = 42,
            CornerRadius = new CornerRadius(12), FontWeight = FontWeight.SemiBold, Background = UrbeTheme.Brush(UrbeTheme.Accent), Foreground = UrbeTheme.Brush("#ffffff")
        };
        ok.Click += (_, _) => { TipMark(Customization, key); HideTip(); };
        bool wide = Bounds.Width >= WideBreakpoint;
        _tip = new Border
        {
            // #urbeTip: 420 px card over the Explorer, above the dock (or beside the rail on wide screens)
            MaxWidth = 420, Padding = new Thickness(20), CornerRadius = new CornerRadius(22), Background = UrbeTheme.Brush(UrbeTheme.Surface, .94),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 18 60 0 #8C000000"),
            VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = wide ? HorizontalAlignment.Left : HorizontalAlignment.Stretch,
            Margin = wide ? new Thickness(24, 0, 0, 104) : new Thickness(16, 0, 16, 88),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeight.Bold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                    new TextBlock { Text = text, FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), Margin = new Thickness(0, 0, 0, 10) },
                    ok
                }
            }
        };
        _stage.Children.Add(_tip);
        TipShown = key;
    }

    private void HideTip()
    {
        if (_tip is not null) _stage.Children.Remove(_tip);
        _tip = null;
        TipShown = null;
    }
}
