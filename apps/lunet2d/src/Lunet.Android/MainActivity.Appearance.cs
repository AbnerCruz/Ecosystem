using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private bool IsUiDemo()
    {
#if LUNET_UI_DEMO
        return !string.IsNullOrEmpty(Intent?.GetStringExtra("lunet.demo"));
#else
        return false;
#endif
    }

    private bool TrySaveWorkspaceSettings(EditorSettings draft)
    {
        try { _settingsStore.Save(draft); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível salvar as configurações: " + ex.Message, ToastLength.Long)?.Show();
            return false;
        }
        _settings = draft;
        return true;
    }

    private bool ApplyWorkspaceTheme()
    {
        var changed = LunetUi.ApplyTheme(this, _settings.Theme);
        Window?.SetStatusBarColor(LunetUi.Background);
        Window?.SetNavigationBarColor(LunetUi.Background);
        if (Window?.DecorView is { } decor)
        {
            var flags = decor.SystemUiFlags & ~(SystemUiFlags.LightStatusBar | SystemUiFlags.LightNavigationBar);
            if (LunetUi.IsLight) flags |= SystemUiFlags.LightStatusBar | SystemUiFlags.LightNavigationBar;
            decor.SystemUiFlags = flags;
        }
        if (changed) _editor?.RefreshPalette();
        return changed;
    }

    private void ShowFirstUse()
    {
        if (IsDestroyed || IsFinishing) return;
        void Advance(int step)
        {
            var draft = _settings.Clone();
            draft.FirstUseStep = step;
            if (TrySaveWorkspaceSettings(draft) && step < 3) ShowFirstUse();
        }

        switch (_settings.FirstUseStep)
        {
            case 0:
                new AlertDialog.Builder(this)!.SetTitle("Onde guardar seus projetos?")!
                    .SetMessage(_store.List().Count > 0
                        ? "Seus jogos já estão no Lunet. Mantenha a pasta atual ou copie os jogos para uma pasta de sua escolha."
                        : "Comece com o armazenamento interno ou escolha uma pasta pelo Android.")!
                    .SetNegativeButton("Usar pasta atual", (_, _) => Advance(1))!
                    .SetPositiveButton("Escolher pasta", (_, _) => PickProjectFolder(fromSetup: true))!.Show();
                break;
            case 1:
                new AlertDialog.Builder(this)!.SetTitle("Aparência do Lunet")!
                    .SetMessage("Escolha um tema e veja a fonte do editor antes de salvar.")!
                    .SetNegativeButton("Manter aparência", (_, _) => Advance(2))!
                    .SetPositiveButton("Personalizar", (_, _) => ShowAppearance(onboarding: true))!.Show();
                break;
            case 2:
                new AlertDialog.Builder(this)!.SetTitle("Configurar IA?")!
                    .SetMessage("O Lunet funciona offline. Você pode configurar o assistente agora ou mais tarde pelo chat global.")!
                    .SetNegativeButton("Agora não", (_, _) => Advance(3))!
                    .SetPositiveButton("Configurar", (_, _) => { Advance(3); ShowAiSettings(); })!.Show();
                break;
        }
    }

    private void ShowAppearance(bool onboarding = false)
    {
        var draft = _settings.Clone();
        var form = LunetUi.Column(this, 8);
        form.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
        var sample = LunetUi.Label(this, "public void Update()\n{\n    player.Move();\n}", draft.FontSize);
        sample.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
        void Preview()
        {
            bool light = LunetUi.UsesLightTheme(this, draft.Theme);
            sample.SetBackgroundColor(light ? Color.Rgb(247, 248, 253) : Color.Rgb(13, 16, 23));
            sample.SetTextColor(light ? Color.Rgb(65, 46, 167) : Color.Rgb(186, 176, 255));
            sample.SetTypeface(LunetUi.EditorTypeface(draft.Font), TypefaceStyle.Normal);
        }

        RadioGroup Choices(string title, string[] labels, int selected, Action<int> choose)
        {
            form.AddView(LunetUi.Label(this, title, LunetUi.TextTitle, bold: true));
            var group = new RadioGroup(this) { Orientation = Orientation.Vertical };
            foreach (var label in labels)
            {
                var button = new RadioButton(this) { Id = View.GenerateViewId(), Text = label };
                button.SetTextColor(LunetUi.Text);
                button.SetMinimumHeight(Dp(48));
                group.AddView(button);
            }
            group.Check(group.GetChildAt(selected)!.Id);
            group.CheckedChange += (_, args) =>
            {
                for (var index = 0; index < group.ChildCount; index++)
                    if (group.GetChildAt(index)?.Id == args.CheckedId) { choose(index); Preview(); break; }
            };
            form.AddView(group);
            return group;
        }

        form.SetBackgroundColor(LunetUi.Surface);
        var themes = Choices("Tema", ["Escuro", "Claro", "Do sistema"], (int)draft.Theme, index => draft.Theme = (WorkspaceTheme)index);
        var fonts = Choices("Fonte do editor", ["Monoespaçada", "Condensada", "Serifada"], (int)draft.Font, index => draft.Font = (EditorFont)index);
        form.AddView(sample);
        form.AddView(LunetUi.Button(this, "Restaurar padrão", () =>
        {
            themes.Check(themes.GetChildAt(0)!.Id);
            fonts.Check(fonts.GetChildAt(0)!.Id);
        }, ButtonStyle.Quiet));
        Preview();

        var scroll = new ScrollView(this);
        scroll.AddView(form);
        var dialog = new AlertDialog.Builder(this)!.SetTitle("Aparência")!.SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { if (onboarding) ShowFirstUse(); })!
            .SetPositiveButton("Salvar", (_, _) =>
            {
                if (onboarding) draft.FirstUseStep = 2;
                if (TrySaveWorkspaceSettings(draft))
                {
                    bool changed = ApplyWorkspaceTheme();
                    ApplyEditorSettings();
                    if (changed && _project is null)
                    {
                        if (_globalChatVisible) ShowWorkspaceAi();
                        else ReturnToLauncherOrigin();
                    }
                }
                if (onboarding) ShowFirstUse();
            })!.Create()!;
        dialog.CancelEvent += (_, _) => { if (onboarding) ShowFirstUse(); };
        dialog.Show();
    }
}
