using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Desktop;

/// <summary>
/// <c>Lunet --screens [pasta]</c>: renders the real shell offscreen (Avalonia headless + Skia) at a phone width and a
/// wide width, through the same calls a person's taps make, and saves PNGs. Every interface PR attaches these
/// (study, Onda 0: "nenhuma tela entra sem captura"). Headless captures are not a device test.
/// </summary>
public static class ShellScreens
{
    public static readonly (string Name, int Width, int Height)[] Sizes = [("celular", 412, 915), ("largo", 1280, 800)];

    public static int Run(string output)
    {
        AppBuilder.Configure<ScreensApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        Directory.CreateDirectory(output);
        var written = new List<string>();
        foreach (var (size, w, h) in Sizes)
        {
            var root = Path.Combine(Path.GetTempPath(), "lunet-screens-" + Guid.NewGuid().ToString("N"));
            try
            {
                var window = new MainWindow(root) { Width = w, Height = h };
                window.Show();
                var shell = window.Shell;
                shell.HardwareKeyboard = size != "celular"; // the phone captures show the code bar
                if (size == "celular") shell.OpenRoute = (_, _) => { }; // show the Android Home destinations
                Shot(window, output, size, "1-home-vazia", written);
                shell.CreateProject();
                shell.GoHome();
                Shot(window, output, size, "2-home", written);
                shell.Home.ShowCreate();
                Shot(window, output, size, "2b-novo-jogo", written);
                shell.Home.CloseSheet();
                shell.Home.ShowActions(shell.Workspace.Projects()[0]);
                Shot(window, output, size, "2c-acoes", written);
                shell.Home.CloseSheet();
                shell.Home.Delete(shell.DuplicateProject(shell.Workspace.Projects()[0]));
                Shot(window, output, size, "2d-desfazer", written);
                shell.Home.ShowTrash();
                Shot(window, output, size, "2e-lixeira", written);
                shell.Home.CloseSheet();
                // A6: the Biblioteca with a pixel-art sprite and a data file published from the game.
                var game = shell.Workspace.Store.Open(shell.Workspace.Projects()[0]);
                Directory.CreateDirectory(Path.Combine(game.Directory, "Content"));
                SamplePng(Path.Combine(game.Directory, "Content", "heroi.png"));
                game.WriteText("Content/fases.json", "{}");
                var lib = new Lunet.Core.ReusableAssetLibrary(shell.LibraryPath);
                lib.Publish(game, "Content/heroi.png", "Herói", "Lunet", "CC0", tags: ["personagem", "pixel art"]);
                lib.Publish(game, "Content/fases.json", "Fases da campanha", "Lunet", "Uso próprio");
                var library = shell.Home.ShowLibrary();
                Shot(window, output, size, "2h-biblioteca", written);
                library.ShowAsset(library.Listed[0].Id, library.Listed[0].Version);
                Shot(window, output, size, "2i-recurso", written);
                shell.Home.CloseSheet(); shell.Home.CloseSheet();
                shell.Home.ShowSettings();
                Shot(window, output, size, "2k-ajustes", written);
                shell.Home.CloseSheet();
                shell.Home.ShowPlay();
                Shot(window, output, size, "2j-jogar", written);
                shell.Home.CloseSheet();
                var learn = shell.Home.ShowLearn();
                Shot(window, output, size, "2f-aprender", written);
                learn.ShowProject("LRN-001-P1");
                Shot(window, output, size, "2g-desafio", written);
                shell.Home.CloseSheet();
                shell.OpenProject(shell.Workspace.Projects()[0]);
                Shot(window, output, size, "3-codigo", written);
                shell.WorkspaceView.Code.ShowKeyboard();
                if (shell.WorkspaceView.Code.Keyboard is { } bar)
                {
                    shell.WorkspaceView.Code.Surface.Caret = 0;
                    Shot(window, output, size, "3b-barra", written);
                    bar.Show(Lunet.Client.Views.CodeBarTab.Shortcuts);
                    Shot(window, output, size, "3c-atalhos", written);
                    bar.Catalog.RecordUse(bar.Catalog.All.First(m => m.Type == "SpriteBatch" && m.Name == "Draw"));
                    bar.Show(Lunet.Client.Views.CodeBarTab.Methods);
                    bar.ToggleNode("Lunet.Input");
                    bar.ToggleNode("Lunet.Input", "InputState");
                    Shot(window, output, size, "3d-metodos", written);
                    bar.Show(Lunet.Client.Views.CodeBarTab.Keys);
                }
                shell.WorkspaceView.Code.HideKeyboard();
                shell.RunGame();
                for (int i = 0; i < 20; i++) shell.TickGame(1 / 60.0);
                Shot(window, output, size, "4-jogo", written);
                shell.Game.TogglePause();
                shell.State.ShowPanel(SupportPanel.Console);
                Shot(window, output, size, "5-console", written);
                shell.StopGame();
                // M6: a game with a few tunable values, as the Inspector and Desempenho panels show it.
                shell.WorkspaceView.Code.Surface.Text = TunableGame;
                shell.RunGame();
                for (int i = 0; i < 90; i++) shell.TickGame(1 / 60.0);
                shell.State.ShowPanel(SupportPanel.Inspector);
                Shot(window, output, size, "5b-inspector", written);
                shell.State.ShowPanel(SupportPanel.Performance);
                Shot(window, output, size, "5c-desempenho", written);
                shell.State.ShowPanel(SupportPanel.Docs);
                shell.WorkspaceView.Support.Docs.Navigate("home");
                Shot(window, output, size, "5d-docs", written);
                shell.WorkspaceView.Support.Docs.OpenSymbol("T:Lunet.Graphics.SpriteBatch", "SpriteBatch");
                Shot(window, output, size, "5e-docs-simbolo", written);
                shell.StopGame();
                shell.WorkspaceView.Code.Surface.Text = "public sealed class Quebrado : Lunet.Game { void X() { int a = ; } }";
                shell.RunGame();
                Shot(window, output, size, "6-problemas", written);
                shell.State.ShowPanel(SupportPanel.None);
                shell.State.SelectTab(WorkspaceTab.Notes);
                Shot(window, output, size, "7-notas", written);
                shell.State.SelectTab(WorkspaceTab.Art);
                var sprite = shell.WorkspaceView.NewSprite();
                sprite.SelectColor(0xFFFFA300);
                for (var y = 4; y < 12; y++) { sprite.Paint(4, y, first: y == 4); sprite.Paint(11, y, first: false); }
                sprite.SelectColor(0xFF29ADFF);
                sprite.SelectTool(Lunet.Client.Views.SpriteTool.Fill);
                sprite.Paint(7, 7, first: true);
                Shot(window, output, size, "8b-sprite", written);
                sprite.TogglePicker();
                Shot(window, output, size, "8c-cor", written);
                sprite.TogglePicker();
                sprite.SelectTool(Lunet.Client.Views.SpriteTool.Select);
                Shot(window, output, size, "8c2-selecionar", written);
                sprite.DragSelect(3, 3, 8, 12);
                sprite.MoveSelection(4, 1);
                Shot(window, output, size, "8c3-selecao-movida", written);
                sprite.CommitFloating();
                shell.Back();
                var tiles = shell.WorkspaceView.NewMap();
                tiles.SelectTile(1);
                for (var x = 0; x < 16; x++) tiles.Paint(x, 12, first: x == 0);
                tiles.Session.EndStroke();
                tiles.SelectTile(3);
                for (var y = 9; y < 12; y++) tiles.Paint(5, y, first: y == 9);
                tiles.Session.EndStroke();
                Shot(window, output, size, "8d-mapa", written);
                tiles.SelectTile(1);
                tiles.UseTerrain();
                Shot(window, output, size, "8d2-terreno", written);
                tiles.SetErase(false);
                tiles.ToggleLayerMenu();
                Shot(window, output, size, "8d3-camada", written);
                tiles.ToggleLayerMenu();
                shell.Back();
                shell.WorkspaceView.NewSound();
                Shot(window, output, size, "8e-som", written);
                shell.Back();
                var mind = shell.WorkspaceView.NewMindMap();
                var enemies = mind.AddChild(mind.Map.Root, "Inimigos");
                mind.AddChild(enemies, "Slime: pula e divide");
                mind.AddChild(enemies, "Morcego: voa em onda");
                var levels = mind.AddChild(mind.Map.Root, "Fases");
                mind.AddChild(levels, "1. Floresta");
                mind.AddChild(mind.Map.Root, "Ideias soltas");
                mind.Rename(mind.Map.Topics[^1].Id, "Ideias soltas");
                Shot(window, output, size, "7b-mapa-mental", written);
                shell.Back();
                shell.WorkspaceView.NewMusic();
                Shot(window, output, size, "8f-musica", written);
                shell.Back();
                var versions = shell.WorkspaceView.OpenGit();
                versions.Start();
                versions.SaveVersion("Primeira versão jogável", "Lunet", "lunet@example.com");
                File.AppendAllText(Path.Combine(shell.Workspace.Project!.Directory, shell.Workspace.Project.Manifest.EntryPoint), "\n// próximo passo");
                versions.Refresh();
                Shot(window, output, size, "9-versoes", written);
                shell.Back();
                var fx = shell.WorkspaceView.NewParticles();
                for (var i = 0; i < 20; i++) fx.Step(0.033);
                Shot(window, output, size, "8g-particulas", written);
                shell.Back();
                var pal = shell.WorkspaceView.OpenPalette();
                pal.UsePreset("floresta");
                pal.AddColors(Lunet.Studio.ProjectPalette.Ramp(0xFF8CC152, 5));
                Shot(window, output, size, "8h-paleta", written);
                shell.Back();
                var data = shell.WorkspaceView.NewData("inimigos");
                data.SetField(0, "id", "slime"); data.SetField(0, "nome", "Slime verde");
                data.AddColumn("vida", Lunet.Studio.Data.DataColumnType.Integer);
                data.AddColumn("voa", Lunet.Studio.Data.DataColumnType.Boolean);
                data.SetField(0, "vida", "12");
                var bat = data.DuplicateRecord(0);
                data.SetField(bat, "id", "morcego"); data.SetField(bat, "nome", "Morcego"); data.SetField(bat, "voa", "true");
                data.Back();
                Shot(window, output, size, "8i-dados", written);
                data.OpenRecord(bat);
                Shot(window, output, size, "8j-registro", written);
                shell.Back();
                shell.Back();
                shell.Back();
                Shot(window, output, size, "8-arte", written);
                window.Close();
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        foreach (var f in written) Console.WriteLine(f);
        return 0;
    }

    private static void Shot(Window window, string output, string size, string name, List<string> written)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var file = Path.Combine(output, $"{size}-{name}.png");
#pragma warning disable CS0618 // plain PNG
        window.CaptureRenderedFrame()?.Save(file);
#pragma warning restore CS0618
        written.Add(file);
    }

    private const string TunableGame = """
        using Lunet; using Lunet.Graphics;
        public enum Humor { Calmo, Agitado }
        public sealed class Bola : Game
        {
            [Group("Bola")] [Range(4, 120)] public int Raio = 36;
            [Range(0, 600)] public float Velocidade = 240;
            public Color Cor = new(165, 148, 255);
            [Group("Cena")] public bool Rastro = true;
            public Humor Humor;
            public string Titulo = "Bola quicando";
            public float X { get; private set; } = 80;
            private float _dx = 1;
            private SpriteBatch _batch = null!;
            protected override void LoadContent() => _batch = new SpriteBatch(GraphicsDevice);
            protected override void Update(GameTime time)
            {
                X += _dx * Velocidade * time.DeltaSeconds;
                if (X > Configuration.VirtualWidth - Raio || X < Raio) _dx = -_dx;
            }
            protected override void Draw(GameTime time)
            {
                GraphicsDevice.Clear(new Color(18, 20, 28));
                _batch.Begin();
                _batch.Circle(new System.Numerics.Vector2(X, Configuration.VirtualHeight / 2f), Raio / 2f, Cor, Raio, 40);
                _batch.End();
            }
        }
        """;

    /// <summary>A 16×16 pixel-art face, so the captures show a real picture.</summary>
    private static void SamplePng(string file)
    {
        using var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(16, 16), new Vector(96, 96), Avalonia.Platform.PixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Unpremul);
        using (var buffer = bitmap.Lock())
        {
            var pixels = new byte[16 * 16 * 4];
            for (var y = 0; y < 16; y++)
                for (var x = 0; x < 16; x++)
                {
                    var i = (y * 16 + x) * 4;
                    var inside = (x - 7.5) * (x - 7.5) + (y - 7.5) * (y - 7.5) < 49;
                    var eye = (y == 6 && (x == 5 || x == 10)) || (y == 10 && x > 4 && x < 11);
                    (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = !inside ? ((byte)0, (byte)0, (byte)0, (byte)0)
                        : eye ? ((byte)20, (byte)12, (byte)51, (byte)255) : ((byte)165, (byte)148, (byte)255, (byte)255);
                }
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        }
#pragma warning disable CS0618 // plain PNG, as the captures
        bitmap.Save(file);
#pragma warning restore CS0618
    }

    private sealed class ScreensApp : LunetApp;
}
