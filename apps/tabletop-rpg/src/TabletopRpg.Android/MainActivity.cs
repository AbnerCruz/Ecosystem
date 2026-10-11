using System.Text.Json;
using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using TabletopRpg.Core;
using TabletopRpg.Persistence;
using TabletopRpg.QuickStart;

namespace TabletopRpg.Android;

/// <summary>
/// Minimal native Android preview for RPG-020/021: C# UI over the existing
/// campaign/engine, not a second rules implementation or a simulated AI.
/// </summary>
[Activity(
    Label = "Mesa RPG",
    MainLauncher = true,
    Exported = true,
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity
{
    private static readonly Color Background = Color.Rgb(16, 23, 34);
    private static readonly Color Surface = Color.Rgb(30, 42, 59);
    private static readonly Color Accent = Color.Rgb(64, 151, 179);
    private static readonly Color Foreground = Color.Rgb(238, 242, 246);
    private static readonly Color Muted = Color.Rgb(176, 192, 204);

    private readonly CampaignCodec _codec = new();
    private Campaign? _campaign;
    private UiCampaignRef? _reference;
    private bool _working;

    private string StoragePath =>
        Path.Combine(FilesDir?.AbsolutePath ?? throw new InvalidOperationException("No application storage."),
            "campaigns");

    private FileCampaignStore Store => new(StoragePath);

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window?.SetStatusBarColor(Background);
        Window?.SetNavigationBarColor(Background);
        RenderHome();
    }

    private void RenderHome()
    {
        _campaign = null;
        _reference = null;
        var page = Page();
        Heading(page, "MESA RPG", "PRÉVIA ANDROID • C# NATIVO");
        Body(page, "Crie uma campanha e jogue o começo de uma sessão sem internet. " +
            "Nesta prévia, Mestre IA e companheiros IA ainda não estão conectados.");
        Subheading(page, "NOVA CAMPANHA");
        Action(page, "Fantasia — Ecos da Ponte Velha", () => BeginAsync(QuickStartTheme.Fantasy));
        Action(page, "Mistério — Teatro Silencioso", () => BeginAsync(QuickStartTheme.Mystery));
        Action(page, "Fronteira — Posto Sete", () => BeginAsync(QuickStartTheme.Frontier));

        Subheading(page, "CAMPANHAS SALVAS");
        var files = Directory.Exists(StoragePath)
            ? Directory.GetFiles(StoragePath, "*.ui.json").OrderByDescending(File.GetLastWriteTimeUtc).ToArray()
            : [];
        var count = 0;
        foreach (var path in files)
        {
            try
            {
                var meta = JsonSerializer.Deserialize<UiCampaignRef>(File.ReadAllText(path));
                if (meta is null || meta.CampaignId == Guid.Empty) continue;
                count++;
                Action(page, meta.Name, () => LoadAsync(meta));
            }
            catch (Exception)
            {
                // A corrupt sidecar must not prevent access to other campaigns.
                Body(page, "Um registro de campanha está ilegível. Outros arquivos não foram alterados.");
            }
        }
        if (count == 0) Body(page, "Nenhuma campanha salva neste dispositivo.");
        Divider(page);
        Body(page, "Dados locais do aplicativo, com gravação atômica e backup de campanha. " +
            "Ainda não há interface de exportação, rede ou configuração de modelos.");
    }

    private async Task BeginAsync(QuickStartTheme theme)
    {
        var started = QuickStartFactory.Create(new QuickStartOptions(theme));
        _campaign = started.Campaign;
        _reference = new UiCampaignRef(
            started.Campaign.Id.Value, started.Campaign.Name,
            started.HumanParticipantId.Value, started.HeroCharacterId.Value);
        await SaveAsync();
        RenderSession();
    }

    private async Task LoadAsync(UiCampaignRef meta)
    {
        var loaded = await Store.LoadAsync(new CampaignId(meta.CampaignId));
        if (!loaded.Campaign.TryGetParticipant(new ParticipantId(meta.HumanParticipantId), out var human) ||
            human.Role != SeatRole.Player || human.Controller != ControllerKind.Human ||
            !human.Controls(new CharacterId(meta.HeroCharacterId)))
            throw new InvalidOperationException("The saved campaign identity is invalid.");

        _campaign = loaded.Campaign;
        _reference = meta;
        RenderSession();
    }

    private async Task SaveAsync()
    {
        if (_campaign is null || _reference is null)
            throw new InvalidOperationException("No campaign selected.");

        await Store.SaveAsync(_campaign);
        Directory.CreateDirectory(StoragePath);
        var target = Path.Combine(StoragePath, _reference.CampaignId.ToString("N") + ".ui.json");
        var temp = target + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(_reference));
        File.Move(temp, target, true);
    }

    private void RenderSession()
    {
        if (_campaign is null || _reference is null)
        {
            RenderHome();
            return;
        }

        var campaign = _campaign;
        var meta = _reference;
        var page = Page();
        Heading(page, campaign.Name, "CAMPANHA LOCAL • SALVAMENTO AUTOMÁTICO");
        Action(page, "← Minhas campanhas", () =>
        {
            RenderHome();
            return Task.CompletedTask;
        });

        var scene = campaign.Scenes.FirstOrDefault(x => x.Id == campaign.ActiveSceneId);
        Subheading(page, scene?.Title ?? "Cena");
        Body(page, scene?.Description ?? "Cena não disponível.");
        var quest = campaign.Quests.FirstOrDefault(x => x.Status == QuestStatus.Open);
        Subheading(page, "OBJETIVO");
        Body(page, quest?.Title ?? "Nenhuma missão em aberto.");

        var hp = campaign.Resources.FirstOrDefault(x => x.CharacterId.Value == meta.HeroCharacterId &&
            x.Key == "hp");
        Body(page, "Sua personagem: " + HeroName(campaign, meta) +
            (hp is null ? "" : $"  •  Vida {hp.Current}/{hp.Maximum}"));

        Subheading(page, "SUAS AÇÕES");
        var message = new EditText(this)
        {
            Hint = "O que você diz?",
            TextSize = 16f
        };
        message.SetTextColor(Foreground);
        message.SetHintTextColor(Muted);
        message.SetSingleLine(false);
        page.AddView(message, Match());
        Action(page, "Falar", async () =>
        {
            var words = (message.Text ?? "").Trim();
            if (words.Length is < 1 or > 500)
                throw new ArgumentException("Escreva entre 1 e 500 caracteres.");
            var engine = CreateEngine(campaign);
            var outcome = engine.Execute(new SpeakIntent(
                new ParticipantId(meta.HumanParticipantId),
                new CharacterId(meta.HeroCharacterId), words));
            if (!outcome.Accepted)
                throw new InvalidOperationException(outcome.Message);
            await SaveAsync();
            RenderSession();
        });
        Action(page, "Testar percepção (d20)", () => CheckAsync("wisdom", "Observar a cena"));
        Action(page, "Testar força (d20)", () => CheckAsync("strength", "Superar um obstáculo"));

        Subheading(page, "SESSÃO");
        if (campaign.ActiveSessionId is not null)
            Action(page, "Encerrar sessão", async () =>
            {
                campaign.EndActiveSession();
                await SaveAsync();
                RenderSession();
            });
        else
            Action(page, "Começar outra sessão", async () =>
            {
                campaign.StartSession(SessionId.New(), "Nova sessão");
                await SaveAsync();
                RenderSession();
            });

        Subheading(page, "HISTÓRICO");
        var recent = campaign.Events
            .Where(x => x.Kind is "speech" or "skill-check" or "gm-narration")
            .TakeLast(12)
            .Reverse()
            .ToArray();
        if (recent.Length == 0)
            Body(page, "Sem ações registradas. Sua primeira decisão começa agora.");
        foreach (var item in recent)
            Body(page, $"#{item.Sequence}  {item.Text}");

        Divider(page);
        Body(page, "Versão de teste: a personagem humana já usa o motor real de ações e dados. " +
            "Mestre e companheiros IA ainda não respondem. Sem multiplayer ou rede.");
    }

    private async Task CheckAsync(string attribute, string action)
    {
        if (_campaign is null || _reference is null)
            throw new InvalidOperationException("No campaign selected.");
        var engine = CreateEngine(_campaign);
        var outcome = engine.Execute(new SkillCheckIntent(
            new ParticipantId(_reference.HumanParticipantId),
            new CharacterId(_reference.HeroCharacterId), attribute, 12, action));
        if (!outcome.Accepted)
            throw new InvalidOperationException(outcome.Message);
        await SaveAsync();
        RenderSession();
        Toast.MakeText(this, outcome.Success ? "Teste bem-sucedido" : "Teste falhou",
            ToastLength.Short)?.Show();
    }

    private static SessionEngine CreateEngine(Campaign campaign) =>
        new(campaign, new BasicD20RuleSystem(), new DiceRoller(new SystemRandomSource()));

    private static string HeroName(Campaign campaign, UiCampaignRef meta) =>
        campaign.TryGetCharacter(new CharacterId(meta.HeroCharacterId), out var hero)
            ? hero.Name : "Personagem";

    private LinearLayout Page()
    {
        var scroll = new ScrollView(this);
        scroll.SetBackgroundColor(Background);
        scroll.FillViewport = true;
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(Dp(20), Dp(22), Dp(20), Dp(32));
        scroll.AddView(root);
        SetContentView(scroll);
        return root;
    }

    private void Heading(LinearLayout root, string name, string sub)
    {
        var title = Label(name, 27, Foreground);
        title.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        root.AddView(title, Match());
        root.AddView(Label(sub, 11, Accent), Match());
        Divider(root);
    }

    private void Subheading(LinearLayout root, string text)
    {
        var view = Label(text, 15, Accent);
        view.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
        view.SetPadding(0, Dp(20), 0, Dp(8));
        root.AddView(view, Match());
    }

    private void Body(LinearLayout root, string text)
    {
        var view = Label(text, 15, Muted);
        view.SetPadding(0, Dp(6), 0, Dp(6));
        root.AddView(view, Match());
    }

    private void Divider(LinearLayout root)
    {
        var view = new View(this);
        view.SetBackgroundColor(Surface);
        var layout = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(1));
        layout.SetMargins(0, Dp(14), 0, Dp(10));
        root.AddView(view, layout);
    }

    private void Action(LinearLayout root, string label, Func<Task> task)
    {
        var button = new Button(this) { Text = label, TextSize = 14f };
        button.SetTextColor(Foreground);
        button.SetBackgroundColor(Surface);
        var layout = Match();
        layout.SetMargins(0, Dp(5), 0, Dp(5));
        root.AddView(button, layout);
        button.Click += async (_, _) =>
        {
            if (_working) return;
            _working = true;
            button.Enabled = false;
            try { await task(); }
            catch (Exception error)
            {
                Toast.MakeText(this, error.Message, ToastLength.Long)?.Show();
            }
            finally
            {
                _working = false;
                button.Enabled = true;
            }
        };
    }

    private TextView Label(string text, float size, Color color)
    {
        var label = new TextView(this) { Text = text, TextSize = size };
        label.SetTextColor(color);
        return label;
    }

    private static LinearLayout.LayoutParams Match() =>
        new(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

    private int Dp(int units) => (int)(units * Resources!.DisplayMetrics!.Density + 0.5f);

    [Serializable]
    private sealed record UiCampaignRef(Guid CampaignId, string Name,
        Guid HumanParticipantId, Guid HeroCharacterId);
}
