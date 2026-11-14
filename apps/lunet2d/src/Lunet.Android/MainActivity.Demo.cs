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
        foreach (var name in new[] { "BosqueLumi", "NaveEstelar" })
            if (!_store.List().Contains(name)) _store.Create(name, ProjectTemplate.CoinCatcher);
        if (screen == "projects") { ShowProjects(); return; }
        OpenProject("BosqueLumi");
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
            case "files": Post(ToggleExplorer); break;
            case "ai-empty":
                AiStore.Save(Lunet.Ai.LunetAiSettings.Default with { Model = "anthropic/claude-sonnet-5" }, "demo-key-not-a-real-secret-0000");
                _ai = null;
                Post(() => OpenAiPanel(null));
                break;
            case "image":
                {
                    using var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(600, 600, global::Android.Graphics.Bitmap.Config.Argb8888!)!;
                    var canvas = new global::Android.Graphics.Canvas(bitmap);
                    canvas.DrawColor(global::Android.Graphics.Color.Rgb(242, 238, 228));
                    var pen = new global::Android.Graphics.Paint(global::Android.Graphics.PaintFlags.AntiAlias) { StrokeWidth = 14, Color = global::Android.Graphics.Color.Rgb(30, 30, 40) };
                    pen.SetStyle(global::Android.Graphics.Paint.Style.Stroke);
                    canvas.DrawCircle(300, 260, 150, pen);
                    canvas.DrawLine(240, 420, 200, 560, pen);
                    canvas.DrawLine(360, 420, 400, 560, pen);
                    pen.SetStyle(global::Android.Graphics.Paint.Style.Fill);
                    canvas.DrawCircle(250, 230, 18, pen);
                    canvas.DrawCircle(350, 230, 18, pen);
                    using var stream = new MemoryStream();
                    bitmap.Compress(global::Android.Graphics.Bitmap.CompressFormat.Jpeg!, 88, stream);
                    stream.Position = 0;
                    _project!.Import(ProjectNotes.Folder, "desenho-do-heroi.jpg", stream);
                    Post(() => OpenImageNote("Notes/desenho-do-heroi.jpg"));
                }
                break;
            case "run": Post(Run); break;
            case "ai":
                AiStore.Save(Lunet.Ai.LunetAiSettings.Default with { Model = "anthropic/claude-sonnet-5" }, "demo-key-not-a-real-secret-0000");
                _ai = null;
                Post(() =>
                {
                    OpenAiPanel(null);
                    DemoConversation();
                });
                break;
            case "problems": Post(() => OpenWorkbenchPanel(false)); break;
        }
    }

    private void Post(Action action) => Window!.DecorView.PostDelayed(action, 600);
}
#endif
