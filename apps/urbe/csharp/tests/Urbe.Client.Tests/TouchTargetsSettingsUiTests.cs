using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    /// <summary>CST-105: the same in Personalização (every section), the Assistente's settings and Páginas; sliders' own track parts are not the app's.</summary>
    public async Task Touch_EveryButtonOfSettingsAndPagesIsBigEnough() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-probe2-" + Guid.NewGuid().ToString("N"));
        var vault = WorkspaceVault(dir);
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(dir, "assistente");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        await Settle(window, view);
        var report = new List<string>();
        void Scan(string screen, Control root)
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            foreach (var b in root.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Bounds.Width > 0 && b.Bounds.Height > 0 && b.FindAncestorOfType<Avalonia.Controls.Primitives.ScrollBar>() is null && b.FindAncestorOfType<Slider>() is null))
                if (b.Bounds.Height < 40 || b.Bounds.Width < 40)
                    report.Add(screen + " | " + (AutomationProperties.GetName(b) ?? ToolTip.GetTip(b) as string ?? (b.Content as string) ?? b.GetType().Name) + " | " + Math.Round(b.Bounds.Width) + "x" + Math.Round(b.Bounds.Height));
        }
        foreach (var section in new[] { "aparencia", "texto", "editor", "estilos", "tags", "plugins", "arquivo" })
        { view.Customize.Open(section); Scan("personalizacao/" + section, view.Customize); }
        view.Customize.Close();
        view.Assistant.Open();
        view.Assistant.ShowSettings();
        foreach (var cat in new[] { "Configurações: Modelos", "Configurações: Agentes", "Configurações: Contexto", "Configurações: Pesquisa" })
        {
            try { ClickNamed(view.Assistant, cat); } catch (Exception) { report.Add("sem " + cat); continue; }
            Scan("assistente/" + cat, view.Assistant);
        }
        view.Assistant.Close();
        view.ShowPages(); await Settle(window, view); Scan("paginas", view);
        window.Close();
        UrbeApp.AssistantFolder = before;
        try { Directory.Delete(dir, true); } catch (IOException) { }
        Assert.True(report.Count == 0, string.Join("\n", report.Distinct()));
        return true;
    }, CancellationToken.None));
}
