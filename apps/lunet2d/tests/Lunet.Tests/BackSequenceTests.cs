using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Tests;

/// <summary>
/// UX14 (anexo A §8.1): Back (Android) and Esc (keyboard) walk every destination back one level at a time:
/// sheet → Home → leave the app; panel → code → Home. Nothing is skipped and nothing traps the person.
/// </summary>
public sealed class BackSequenceTests
{
    [Fact]
    public void BackAndEsc_CloseEachDestination_OneLevelAtATime()
    {
        var root = Directory.CreateTempSubdirectory("lunet-back-").FullName;
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
                void Pump() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                shell.CreateProject();
                shell.GoHome(); Pump();
                var game = shell.Workspace.Projects()[0];

                var openers = new (string Name, Action Open)[]
                {
                    ("Jogar", () => shell.Home.ShowPlay()), ("Aprender", () => shell.Home.ShowLearn()),
                    ("Studios", () => shell.Home.ShowStudios()), ("Documentação", () => shell.Home.ShowDocs()),
                    ("Ajustes", () => shell.Home.ShowSettings()), ("IA", () => shell.Home.ShowGlobalAi()),
                    ("Ajustes da IA", () => shell.Home.ShowGlobalAi(settings: true)), ("Biblioteca", () => shell.Home.ShowLibrary()),
                    ("Novo jogo", shell.Home.ShowCreate), ("Ações do jogo", () => shell.Home.ShowActions(game)),
                    ("Lixeira", shell.Home.ShowTrash),
                };
                foreach (var (name, open) in openers)
                    foreach (var esc in new[] { false, true })
                    {
                        open(); Pump();
                        Assert.True(shell.Home.Sheet is not null, name + " não abriu");
                        if (name == "Ajustes da IA")
                        {   // settings live inside the AI: one level up is the conversation, then Home
                            Assert.True(shell.Back()); Pump();
                            var ai = Assert.IsType<Lunet.Client.Views.GlobalAiView>(shell.Home.Sheet);
                            Assert.False(ai.ShowingSettings);
                        }
                        if (esc) window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                        else Assert.True(shell.Back(), name + ": Voltar não fez nada");
                        Pump();
                        Assert.True(shell.Home.Sheet is null, $"{name}: {(esc ? "Esc" : "Voltar")} não fechou");
                        Assert.Equal(ShellLevel.Home, shell.State.Level);
                    }

                // In the game: panel → code → Home.
                shell.OpenProject(game); Pump();
                foreach (var panel in new[] { SupportPanel.Ai, SupportPanel.Objectives })
                {
                    shell.State.ShowPanel(panel); Pump();
                    Assert.True(shell.Back());
                    Assert.Equal(SupportPanel.None, shell.State.Panel);
                    Assert.Equal(ShellLevel.Workspace, shell.State.Level);
                }
                Assert.True(shell.Back()); Pump();
                Assert.Equal(ShellLevel.Home, shell.State.Level);
                Assert.False(shell.Back());                                   // at Home, Back leaves the app (Android handles it)
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
