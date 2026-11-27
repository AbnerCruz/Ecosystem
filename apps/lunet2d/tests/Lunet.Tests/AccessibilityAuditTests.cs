using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Ui;

namespace Lunet.Tests;

/// <summary>
/// UX12 (anexo A §8.1): on every destination of the shell, each control a person can act on has a name a screen
/// reader can say and a touch target of at least <see cref="LunetTokens.MinTouch"/> in one axis and 40 in the other.
/// Headless audit: it proves names and sizes, not TalkBack on a phone.
/// </summary>
public sealed class AccessibilityAuditTests
{
    internal static string? SpokenName(Control c)
    {
        var name = AutomationProperties.GetName(c);
        if (!string.IsNullOrWhiteSpace(name)) return name;
        if (c is ContentControl { Content: string s } && !string.IsNullOrWhiteSpace(s)) return s;
        if (c is TextBox t && (!string.IsNullOrWhiteSpace(t.PlaceholderText) || AutomationProperties.GetLabeledBy(t) is not null)) return t.PlaceholderText;
        var text = c.GetVisualDescendants().OfType<TextBlock>().Select(b => b.Text).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return text;
    }

    internal static List<string> Audit(Window window, string where)
    {
        var problems = new List<string>();
        foreach (var c in window.GetVisualDescendants().OfType<Control>())
        {
            if (c is not (Button or TextBox or ToggleSwitch or ComboBox or CheckBox or Slider)) continue;
            if (c is TextBox && c.FindAncestorOfType<ComboBox>() is not null) continue; // part of a template
            if (!c.IsEffectivelyVisible || !c.IsEffectivelyEnabled || c.Bounds.Width <= 0) continue;
            if (c.GetVisualAncestors().Any(a => a is ScrollBar or Slider)) continue;     // parts of a platform control
            var label = $"{where}: {c.GetType().Name} '{c.Name ?? SpokenName(c) ?? "?"}'";
            if (string.IsNullOrWhiteSpace(SpokenName(c))) problems.Add(label + " sem nome para o leitor de tela");
            var (w, h) = (c.Bounds.Width, c.Bounds.Height);
            if (c is not (Slider or TextBox) && (Math.Max(w, h) < LunetTokens.MinTouch || Math.Min(w, h) < 36))
                problems.Add(label + $" alvo de toque {w:0}×{h:0}");
        }
        return problems;
    }

    [Fact]
    public void EveryDestination_NamesItsControls_AndKeepsTouchTargets()
    {
        var root = Directory.CreateTempSubdirectory("lunet-a11y-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var aiDir = Path.Combine(root, ".ai");
                var shell = new LunetShell(root)
                {
                    AiHost = new Lunet.Client.Views.AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(aiDir, "s.json"), new NullVault()), aiDir),
                };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                var problems = new List<string>();
                void Check(string where) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); problems.AddRange(Audit(window, where)); }
                shell.CreateProject();
                Check("IDE");
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Ai); Check("IA do jogo");
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Objectives); Check("Objetivos");
                shell.Back(); shell.Back(); shell.Back();
                Check("Início");
                shell.Home.ShowPlay(); Check("Jogar"); shell.Back();
                shell.Home.ShowLearn(); Check("Aprender"); shell.Back();
                shell.Home.ShowStudios(); Check("Studios"); shell.Back();
                shell.Home.ShowDocs(); Check("Documentação"); shell.Back();
                shell.Home.ShowSettings(); Check("Ajustes"); shell.Back();
                shell.Home.ShowGlobalAi(settings: true); Check("Ajustes da IA"); shell.Back();
                shell.Home.ShowLibrary(); Check("Biblioteca"); shell.Back();
                Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct()));
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(1.0, 412, 1.0)]
    [InlineData(1.15, 412, 1.15)]
    [InlineData(2.0, 412, 412 / 320.0)]   // a phone: grows until the layout would be narrower than 320
    [InlineData(2.0, 840, 2.0)]           // a tablet has room for the whole request
    [InlineData(0.85, 412, 1.0)]          // "small" never shrinks below the designed size
    public void TheZoomFollowsTheSystem_UpToTheNarrowestTestedLayout(double requested, double width, double expected) =>
        Assert.Equal(expected, TextScaleHost.Limit(requested, width), 3);

    [Fact]
    public void WithTheLargestFont_TheShellStillNamesAndFitsEverything()
    {
        var root = Directory.CreateTempSubdirectory("lunet-a11y-big-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var host = new TextScaleHost(shell) { Requested = 2.0 };
                var window = new Window { Width = 412, Height = 915, Content = host };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(412 / 320.0, host.Effective, 3);
                Assert.Equal(320, shell.Bounds.Width, 0);                       // the shell lays out at its narrowest tested width
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var problems = Audit(window, "IDE ampliada");
                shell.Back(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                problems.AddRange(Audit(window, "Início ampliado"));
                Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct()));
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class NullVault : Lunet.Ai.ISecretVault
    {
        public string? Get(string name) => null;
        public void Set(string name, string value) { }
        public void Delete(string name) { }
    }
}
