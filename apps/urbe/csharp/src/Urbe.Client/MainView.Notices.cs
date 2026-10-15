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

    // ---------------------------------------------------------------- tips.js

    private Border? _tip;
    private int? _tipLinksBefore;

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
        Explorer.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && Explorer.IsVisible) HideTip(); };
        NoteSaved += _ => CheckFirstRoad();
    }

    /// <summary>workspace:loaded: how many links there were (the first road is noticed from here).</summary>
    private void TipsLoaded() => _tipLinksBefore = TipSeen(Customization, "road") ? null : LinkCount();

    /// <summary>1) back in the city with notes but no link yet: "Ligue suas notas".</summary>
    public void MaybeLinkTip()
    {
        if (Editor.IsVisible || Explorer.IsVisible || _store is null || _store.List().Count < 1) return;
        if (TipSeen(Customization, "link") || LinkCount() > 0) return;
        ShowTip("link", "Ligue suas notas", "Dentro de uma nota, digite [[ e escolha outra nota. Uma rua aparece ligando as duas casas.");
    }

    /// <summary>2) the first road was built: a notice once.</summary>
    private void CheckFirstRoad()
    {
        if (_tipLinksBefore is not { } before || TipSeen(Customization, "road")) return;
        var n = LinkCount();
        if (before == 0 && n > 0)
        {
            TipMark(Customization, "road");
            TipMark(Customization, "link");
            HideTip();
            ShowLifeNotice("Primeira rua construída! Volte à cidade para ver a ligação.");
        }
        _tipLinksBefore = n;
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
            // #urbeTip: 420 px card over the city, above the dock (or beside the rail on wide screens)
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
