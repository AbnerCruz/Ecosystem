using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// "IA no aparelho" (24/10/2026): the model that lives on the device and answers without the internet. One screen to
/// download it (resuming, checked), use it and take it away; once it is installed it is a provider like the others.
/// </summary>
public sealed partial class AssistantPanel
{
    private readonly Dictionary<string, CancellationTokenSource> _downloads = [];
    private readonly Dictionary<string, (long Done, long Total)> _deviceProgress = [];

    /// <summary>True on a device that has the offline AI.</summary>
    public static bool HasDeviceAi => LocalAi.Text is not null;

    /// <summary>Installed and chosen: the Assistente answers on the device.</summary>
    private bool UsingDevice => Provider()?.Preset == "device";

    public void ShowDevice()
    {
        if (LocalAi.Text is not { } runtime) { _app.Notify("Este aparelho não tem a IA no aparelho."); return; }
        var body = OpenView("IA no aparelho", () => ShowSettings());
        body.Children.Add(Wrap("Uma IA que mora no seu aparelho e responde sem internet, sem chave e sem custo. Nada do que você escreve sai daqui. O modelo é baixado uma vez (usa dados; use o Wi-Fi) e depois funciona sempre, até no modo avião.", 14, UrbeTheme.Text2));
        var list = new StackPanel { Spacing = 12, Margin = new Thickness(0, 6, 0, 0) };
        body.Children.Add(list);
        body.Children.Add(Hint("Respostas de um modelo pequeno são mais simples que as de modelos na nuvem e levam alguns segundos para começar, mais ainda no primeiro uso, quando o modelo é carregado na memória."));

        void Paint()
        {
            list.Children.Clear();
            var ordered = runtime.Models.OrderByDescending(m => m.Id == runtime.Recommended.Id).ThenBy(m => m.Bytes).ToList();
            foreach (var m in ordered) list.Children.Add(DeviceCard(runtime, m, Paint, m.Id == runtime.Recommended.Id));
        }
        Paint();
    }

    private Control DeviceCard(ILocalTextRuntime runtime, LocalModel model, Action repaint, bool recommended)
    {
        var card = new StackPanel { Spacing = 8 };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        title.Children.Add(Label(model.Name, 16, UrbeTheme.Text, FontWeight.Bold));
        if (recommended)
            title.Children.Add(new Border
            {
                Background = UrbeTheme.Brush(UrbeTheme.Accent, .16), CornerRadius = new CornerRadius(999), Padding = new Thickness(9, 2), VerticalAlignment = VerticalAlignment.Center,
                Child = Label("Recomendado para este aparelho", 11, UrbeTheme.Accent, FontWeight.SemiBold)
            });
        card.Children.Add(title);
        card.Children.Add(Wrap(model.Detail, 13, UrbeTheme.Text2));
        card.Children.Add(Hint("Ocupa " + model.SizeText + " no aparelho · responde com ferramentas (lê e edita suas notas)"));

        bool installed = runtime.IsInstalled(model);
        bool inUse = installed && UsingDevice && _cfg.ModelFor(_conv).Model == model.Id;
        if (_downloads.ContainsKey(model.Id))
        {
            var (done, total) = _deviceProgress.GetValueOrDefault(model.Id, (0, model.Bytes));
            var bar = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, total), Value = done, Height = 6, CornerRadius = new CornerRadius(3), Foreground = UrbeTheme.Brush(UrbeTheme.Accent) };
            var text = Hint(ProgressText(done, total));
            card.Children.Add(bar);
            card.Children.Add(text);
            _progressViews[model.Id] = (bar, text);
            card.Children.Add(TextButton("Pausar", () => { if (_downloads.TryGetValue(model.Id, out var cts)) cts.Cancel(); }));
        }
        else if (installed)
        {
            card.Children.Add(Label(inUse ? "Em uso" : "Instalado", 12, "#3a9a5b", FontWeight.SemiBold));
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (!inUse) row.Children.Add(TextButton("Usar esta IA", () => _ = UseDeviceAsync(runtime, model), primary: true));
            row.Children.Add(TextButton("Remover do aparelho", () => _ = RemoveDeviceAsync(runtime, model, repaint), danger: true));
            card.Children.Add(row);
        }
        else
        {
            var partial = runtime is IPartialDownloads p ? p.PartialBytes(model) : 0;
            if (partial > 0) card.Children.Add(Hint("Já baixado: " + (100 * partial / model.Bytes) + "%"));
            card.Children.Add(TextButton(partial > 0 ? "Continuar o download (" + model.SizeText + ")" : "Baixar (" + model.SizeText + ")", () => _ = DownloadAsync(runtime, model, repaint), primary: true));
        }
        return new Border
        {
            Child = card, Padding = new Thickness(16, 14), CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1),
            BorderBrush = UrbeTheme.Brush(recommended ? UrbeTheme.Accent : UrbeTheme.Line2, recommended ? .5 : 1), Background = UrbeTheme.Brush(UrbeTheme.Surface)
        };
    }

    private readonly Dictionary<string, (ProgressBar Bar, TextBlock Text)> _progressViews = [];

    private static string ProgressText(long done, long total) =>
        (done / 1e9).ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " GB de " + (total / 1e9).ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " GB · " + (100 * done / Math.Max(1, total)) + "%";

    private async Task DownloadAsync(ILocalTextRuntime runtime, LocalModel model, Action repaint)
    {
        var cts = new CancellationTokenSource();
        _downloads[model.Id] = cts;
        _deviceProgress[model.Id] = (0, model.Bytes);
        repaint();
        var last = 0L;
        var progress = new Progress<(long Done, long Total)>(p =>
        {
            _deviceProgress[model.Id] = p;
            if (p.Done - last < 2_000_000 && p.Done < p.Total) return;   // a few updates per second are enough
            last = p.Done;
            if (_progressViews.TryGetValue(model.Id, out var v)) { v.Bar.Value = p.Done; v.Text.Text = ProgressText(p.Done, p.Total); }
        });
        try
        {
            await runtime.InstallAsync(model, progress, cts.Token);
            _app.Notify(model.Name + " está pronta neste aparelho.");
        }
        catch (OperationCanceledException) { _app.Notify("Download pausado. Toque em Continuar para seguir de onde parou."); }
        catch (Exception e) { await _app.Dialogs.AlertAsync("Não foi possível baixar", e.Message); }
        finally
        {
            _downloads.Remove(model.Id);
            _progressViews.Remove(model.Id);
            cts.Dispose();
        }
        if (runtime.IsInstalled(model) && !_cfg.Providers.Any(p => p.Preset == "device")) await UseDeviceAsync(runtime, model, close: false);
        repaint();
    }


    private async Task UseDeviceAsync(ILocalTextRuntime runtime, LocalModel model, bool close = true)
    {
        var cfg = _cfg.Providers.FirstOrDefault(p => p.Preset == "device") ?? new AiProviderConfig("device", "device", "IA no aparelho", "", "");
        if (!_cfg.Providers.Any(p => p.Id == cfg.Id)) _cfg.Providers.Add(cfg);
        _cfg.ModelCache[cfg.Id] = new AiModelCache(Now(), await Providers.ListModelsAsync(cfg));
        _cfg.ProviderId = cfg.Id;
        _cfg.Model = model.Id;
        _conv.ProviderId = cfg.Id;
        _conv.Model = model.Id;
        SaveConfig();
        if (close) { CloseView(); RenderAll(); _app.Notify("O Assistente agora responde no aparelho, sem internet."); }
        else RenderAll();
    }

    private async Task RemoveDeviceAsync(ILocalTextRuntime runtime, LocalModel model, Action repaint)
    {
        if (!await _app.Dialogs.ConfirmAsync("Remover " + model.Name + "?", "Libera " + model.SizeText + ". Você pode baixar de novo quando quiser.", "Remover", danger: true)) return;
        runtime.Remove(model);
        if (_cfg.Providers.FirstOrDefault(p => p.Preset == "device") is { } cfg && !runtime.Models.Any(runtime.IsInstalled))
        {
            _cfg.Providers.RemoveAll(p => p.Id == cfg.Id);
            _cfg.ModelCache.Remove(cfg.Id);
            if (_cfg.ProviderId == cfg.Id) { _cfg.ProviderId = _cfg.Providers.FirstOrDefault()?.Id; _cfg.Model = ""; _conv.ProviderId = _cfg.ProviderId; _conv.Model = ""; }
            SaveConfig();
            RenderAll();
        }
        repaint();
    }
}
