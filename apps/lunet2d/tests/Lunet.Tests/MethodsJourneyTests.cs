using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>
/// UX03 (anexo A §8.1) as one journey: in the game's code, the bar's Métodos tab inserts a Framework member with its
/// type, the analysis shows its signature while typing a call, and the game compiles and runs with the inserted code.
/// </summary>
public sealed class MethodsJourneyTests
{
    private static void Type(CodeBar bar, int at, string text) => bar.Apply(new CodeEdit(at, at, text, at + text.Length));

    [Fact]
    public void MetodosInsertsAMember_TheSignatureShows_AndTheGameRunsWithIt()
    {
        var root = Directory.CreateTempSubdirectory("lunet-methods-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                void Pump(int ms = 0)
                {
                    var until = DateTime.UtcNow.AddMilliseconds(ms);
                    do { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } while (DateTime.UtcNow < until);
                }
                var shell = new LunetShell(root) { HardwareKeyboard = false };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                Pump();
                var editor = shell.WorkspaceView.Code;
                var surface = editor.Surface;
                surface.Focus();
                editor.ShowKeyboard();
                Pump();
                var bar = Assert.IsType<CodeBar>(editor.Keyboard);

                // Inside Draw, right after Begin: "var cor = " then Métodos → Color.Red.
                var anchor = surface.Text.IndexOf("Begin(", StringComparison.Ordinal);
                Assert.True(anchor > 0, "o modelo não tem Begin(");
                var lineEnd = surface.Text.IndexOf('\n', anchor);
                surface.Caret = lineEnd;
                Type(bar, lineEnd, "\n        var cor = ");
                bar.Show(CodeBarTab.Methods);
                var red = bar.Catalog.All.First(m => m.Type == "Color" && m.Name == "Red");
                bar.Insert(red);
                Assert.Contains("var cor = Color.Red", surface.Text);             // static member: written with its type
                Assert.Equal(CodeBarTab.Keys, bar.Tab);                              // back to typing
                Assert.Contains(bar.Catalog.Recent(), m => m.Name == "Red");         // Recentes first next time

                // Typing a call shows its signature (the analysis runs off the keystroke).
                Type(bar, surface.Caret, ";\n        batch.Draw(");
                // The signature comes from the analysis, which offers completions only while the keyboard (and its bar)
                // is up; it is awaited here instead of its 350 ms debounce, so the runner's speed does not matter.
                surface.Focus();
                editor.ShowKeyboard();
                Pump();
                Assert.True(editor.KeyboardVisible, "o teclado e a barra deviam estar abertos para a assinatura");
                // The typing timer's analysis and this one cancel each other (the newest edit wins), so wait for the
                // signature itself, asking again whenever no analysis is pending.
                Task? pending = null;
                var signatureBy = DateTime.UtcNow.AddSeconds(60);
                while (string.IsNullOrEmpty(bar.SignatureText) && DateTime.UtcNow < signatureBy)
                {
                    if (pending is null || pending.IsCompleted) pending = editor.AnalyzeNow();
                    Pump(50);
                }
                Assert.False(string.IsNullOrEmpty(bar.SignatureText), "sem assinatura enquanto digita a chamada");
                Assert.Contains("Draw(", bar.SignatureText);
                Type(bar, surface.Caret, "new RectangleF(0, 0, 8, 8), cor);");   // Anexo B: Draw(RectangleF, Color)

                shell.RunGame();
                var until = DateTime.UtcNow.AddSeconds(30);
                while (shell.Game.State is not (GameState.Running or GameState.CompileErrors or GameState.Faulted) && DateTime.UtcNow < until) Pump(20);
                Assert.True(shell.Game.State == GameState.Running, string.Join("\n", shell.Game.Problems));
                shell.StopGame();
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }
}
