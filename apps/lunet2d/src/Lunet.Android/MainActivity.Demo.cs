#if LUNET_UI_DEMO
using Android.Content;
using Lunet.Core;
using Lunet.Studio;

namespace Lunet.Android;

/// <summary>
/// Screenshot harness for CI (tools/ci-ui-screens.sh): <c>am start --es lunet.demo &lt;screen&gt;</c> opens a screen
/// with sample content so the UI can be reviewed on an emulator. Compiled only with -p:LunetUiDemo=true.
/// </summary>
public sealed partial class MainActivity
{
    private void RunDemo(Intent? intent)
    {
        var screen = intent?.GetStringExtra("lunet.demo");
        if (string.IsNullOrEmpty(screen)) return;
        foreach (var name in new[] { "Bosque Lumi", "Nave Estelar" })
            if (!_store.List().Contains(name)) _store.Create(name, ProjectTemplate.CoinCatcher);
        if (screen == "projects") { ShowProjects(); return; }
        OpenProject("Bosque Lumi");
        switch (screen)
        {
            case "palette": Post(ShowCommandPalette); break;
            case "studio": Post(ShowStudioHub); break;
            case "ai-settings": Post(() => ShowAiSettings()); break;
            case "notes":
                if (ProjectNotes.List(_project!).Count == 0)
                {
                    var plan = ProjectNotes.CreateNote(_project!, "Plano da fase 1");
                    _project!.WriteText(plan, "# Plano da fase 1\n\n- [x] Movimento do herói\n- [ ] Inimigos patrulhando\n- [ ] Chefe no fim\n\n**Ideia:** cogumelos que brilham à noite.\n");
                    ProjectNotes.CreateNote(_project!, "Roteiro");
                }
                Post(ShowNotes);
                break;
            case "note":
                var path = ProjectNotes.CreateNote(_project!, "Plano da fase 1");
                _project!.WriteText(path, "# Plano da fase 1\n\n- [x] Movimento do herói\n- [ ] Inimigos patrulhando\n\n**Ideia:** cogumelos que brilham.\n");
                Post(() => OpenNote(path));
                break;
            case "sprite":
                Post(() => OpenSpriteStudio("Assets/demo.sprite.json", SpriteDocument.Create(32)));
                break;
            case "ai":
                Post(() =>
                {
                    OpenAiPanel(null);
                    AddAiBubble("Você", "Faça o herói pular quando tocar na tela.");
                    AddAiBubble("Lunet", "Pronto. Adicionei gravidade e um impulso ao tocar:\n\n```csharp\nif (Input.Touch.Pressed && _onGround)\n    _velocity.Y = -JumpSpeed;\n_velocity.Y += Gravity * dt;\n```\n\nCompilei o projeto e não há erros.");
                });
                break;
            case "problems": Post(() => OpenWorkbenchPanel(false)); break;
        }
    }

    private void Post(Action action) => Window!.DecorView.PostDelayed(action, 600);
}
#endif
