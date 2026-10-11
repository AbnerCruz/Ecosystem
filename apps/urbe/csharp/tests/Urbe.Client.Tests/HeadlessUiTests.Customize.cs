using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>Personalização sections (Aparência, Texto, Editor, Texturas, Estilos, Plugins) driven headlessly; the effect on the app is asserted, not just the controls.</summary>
public sealed partial class HeadlessUiTests
{
    private static async Task<(MainView View, Window Window, string Dir, string Vault)> VaultViewAsync(Dictionary<string, string>? files = null, double width = 1280, double height = 760)
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-customize-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        foreach (var (path, text) in files ?? [])
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(vault, path))!);
            File.WriteAllText(Path.Combine(vault, path), text);
        }
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        view.Customization.Device = new MemoryDeviceStore();
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        await Settle(window, view);
        return (view, window, dir, vault);
    }

    private static void ResetTheme(MainView view)
    {
        UrbeTheme.SetColors(new Dictionary<string, string>());
        if (Application.Current is { } app) app.RequestedThemeVariant = ThemeVariant.Dark;
    }

    private static IEnumerable<string> Texts(Control root) => root.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "");

    [Fact]
    public async Task Customize_ThemeColorsFontsAndShapeReachTheRunningApp() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, vault) = await VaultViewAsync();
        try
        {
            Assert.Equal("#0e0f11", UrbeTheme.Current(UrbeTheme.Bg));
            var bg = (Avalonia.Media.SolidColorBrush)UrbeTheme.Brush(UrbeTheme.Bg);
            view.Customize.Open("aparencia");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Todas as cores (14)", Texts(view.Customize));
            ClickButtonWithText(view.Customize, "Oceano");
            await Task.Delay(50); Dispatcher.UIThread.RunJobs();
            // the shared token brush (used by every control that already exists) changed colour
            Assert.Equal(Avalonia.Media.Color.Parse("#081317"), bg.Color);
            Assert.Equal("#081317", UrbeTheme.Current(UrbeTheme.Bg));
            Assert.Contains("\"tema\": \"oceano\"", File.ReadAllText(Path.Combine(vault, "Personalização", "tema.json")));

            // a light theme flips the Fluent variant; an own accent colour feeds the accent token
            await view.Customization.SetAsync(new JsonObject { ["tema"] = "claro", ["cores"] = new JsonObject { ["destaque"] = "#ffd400" } });
            Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
            Assert.Equal("#ffd400", UrbeTheme.Current(UrbeTheme.Accent));
            Assert.Equal("#0b1325", UrbeTheme.Current(UrbeTheme.AccentInk));   // dark ink on a bright accent

            // corners: retos → the panel card (--ui-r-xl) goes from 22 to 8
            await view.Customization.SetAsync(new JsonObject { ["forma"] = new JsonObject { ["cantos"] = "retos", ["densidade"] = "compacta" }, ["animacoes"] = "reduzidas" });
            Dispatcher.UIThread.RunJobs();
            var card = view.Customize.GetLogicalDescendants().OfType<Border>().FirstOrDefault(b => b.CornerRadius.TopLeft == 8 && b.Width >= 900);
            Assert.NotNull(card);
            Assert.NotNull(card);
            Assert.Equal("compacta", UrbeAppearance.Density);
            Assert.True(UrbeAppearance.ReduceMotion);
            Assert.Equal(44, UrbeAppearance.DialogOption.MinHeight);

            // text: font family/size and the editor column
            await view.Customization.SetAsync(new JsonObject { ["texto"] = new JsonObject { ["fonte"] = "serifada", ["tamanho"] = 18, ["tamanhoEditor"] = 20, ["alturaLinha"] = 2 }, ["editor"] = new JsonObject { ["largura"] = "larga" } });
            Assert.StartsWith("Iowan Old Style", UrbeTheme.UiFont.Name);
            Assert.Equal(18, Avalonia.Controls.Documents.TextElement.GetFontSize(view));
            Assert.Equal(920, view.Editor.Editor.MaxWidth);
            Assert.Equal(Math.Round(20 * .88), view.Editor.Editor.FontSize);
            Assert.Equal(Math.Round(20 * .88) * 2, view.Editor.Editor.LineHeight);
            ClickButtonWithText(view.Customize, "Texto");
            Assert.Contains("FONTE DA INTERFACE", Texts(view.Customize));
            Assert.Contains("Exemplo de nota", Texts(view.Customize));
            Snapshot(window, "native-customize-text.png");

            // the vault keeps all of it; restoring the default restores the app
            await view.Customization.ResetAsync();
            Assert.Equal("#0e0f11", UrbeTheme.Current(UrbeTheme.Bg));
            Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
            Assert.Equal(720, view.Editor.Editor.MaxWidth);
            Assert.Equal(22, view.Customize.GetLogicalDescendants().OfType<Border>().First(b => b.Width >= 900).CornerRadius.TopLeft);
            return true;
        }
        finally { ResetTheme(view); window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Customize_AllSectionsRenderTheirControls() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, _) = await VaultViewAsync(width: 412, height: 860);
        try
        {
            view.Customize.Open();
            var expect = new Dictionary<string, string[]>
            {
                ["aparencia"] = ["Tema", "Cor de destaque", "Cores", "Forma", "Cantos", "Densidade", "Efeito de vidro", "Animações"],
                ["texto"] = ["Fonte da interface", "Fonte das notas", "Tamanho", "Altura da linha"],
                ["editor"] = ["Editor de notas", "Largura do texto", "Abrir notas no modo", "Corretor ortográfico"],
                ["cidade"] = ["Vida na cidade", "Luz", "Cores do chão", "Grama", "Mar profundo"],
                ["texturas"] = ["Pacote de texturas"],
                ["estilos"] = ["Estilos", "CSS rápido"],
                ["plugins"] = ["Plugins desta cidade", "Segurança", "Modo seguro"],
                ["arquivo"] = ["Personalização/tema.json"]
            };
            foreach (var (section, wanted) in expect)
            {
                view.Customize.Open(section);
                Dispatcher.UIThread.RunJobs();
                var texts = Texts(view.Customize).Select(t => t.ToLowerInvariant()).ToList();
                foreach (var w in wanted) Assert.True(texts.Contains(w.ToLowerInvariant()), section + " sem \"" + w + "\"");
            }
            view.Customize.Open("estilos");
            Snapshot(window, "native-customize-styles-phone.png");
            Assert.Contains(Texts(view.Customize), t => t.Contains("não tem CSS"));
            view.Customize.Open("plugins");
            Assert.Contains(Texts(view.Customize), t => t.Contains("não executa plugins"));
            return true;
        }
        finally { ResetTheme(view); window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Customize_GroundColorsAndTexturePackRepaintTheWorld() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, vault) = await VaultViewAsync();
        try
        {
            Assert.Null(view.World.Ground);
            // Cores do chão: every biome magenta → the near ground of the city turns magenta
            var all = new JsonObject();
            foreach (var name in new[] { "grama", "prado", "floresta", "mata", "pantano", "taiga", "tundra", "neve", "colinas", "montanha", "pico", "deserto", "savana", "estepe", "praia", "agua" })
                all[name] = new JsonArray("#ff00ff");
            await view.Customization.SetAsync(new JsonObject { ["cidade"] = new JsonObject { ["paleta"] = all } });
            Assert.NotNull(view.World.Ground);
            Assert.Equal("#ff00ff", view.World.Ground!.BaseColor("grass"));
            Assert.Equal("#ff00ff", view.World.Ground!.BaseColor("deep"));
            Assert.Equal(0, view.World.ReadyChunks);   // the old ground is gone, chunks are painted again
            await Settle(window, view);
            for (int i = 0; i < 10; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame(); await Task.Delay(20); }
            Snapshot(window, "native-customize-ground-magenta.png");
            Assert.True(MagentaShare(window) > .25, "o chão deveria ficar magenta, share=" + MagentaShare(window));

            await view.Customization.ResetSectionAsync("cidade");
            Assert.Null(view.World.Ground);

            // a texture pack with an own 16×16 grass: palette + pixels reach the ground and the building art
            var row = string.Concat(Enumerable.Repeat("ab", 8));
            var pack = new JsonObject
            {
                ["nome"] = "Teste",
                ["chao"] = new JsonObject { ["grama"] = new JsonObject { ["cores"] = new JsonObject { ["a"] = "#102030", ["b"] = "#405060" }, ["pixels"] = new JsonArray(Enumerable.Range(0, 16).Select(_ => (JsonNode)row).ToArray()) } },
                ["construcoes"] = new JsonObject { ["casa"] = new JsonObject { ["cores"] = new JsonObject { ["x"] = "#ff0000" }, ["pixels"] = new JsonArray("xx", "xx") } }
            };
            var packPath = "Personalização/texturas/Teste.json";
            await view.Customization.UpsertAsync(packPath, LegacyCustomize.Stringify(pack, 2) + "\n");
            Assert.True(File.Exists(Path.Combine(vault, "Personalização", "texturas", "Teste.json")));
            await view.Customization.SetAsync(new JsonObject { ["cidade"] = new JsonObject { ["paleta"] = new JsonObject(), ["texturas"] = packPath } });
            Assert.Equal([0x10, 0x20, 0x30, 255], view.World.Ground!.Textures[Array.IndexOf(LegacyWorldPixelTextures.Biomes.ToArray(), "grass")][0].Take(4));
            Assert.Equal([0x40, 0x50, 0x60, 255], view.World.Ground!.Textures[Array.IndexOf(LegacyWorldPixelTextures.Biomes.ToArray(), "grass")][2].Skip(4).Take(4));
            Assert.True(view.Customization.BuildingArt.ContainsKey("house"));
            Assert.Equal(2, view.Customization.BuildingArt["house"].Width);
            var house = view.World.City!.Buildings.First(b => b.Ext is ".md" or "md");
            var art = view.World.ArtOf(house)!;
            Assert.Equal(2, art.PixelSize.Width);   // urbeSpritesProprios[house]

            // the Texturas section lists the pack, the cells and what the pack owns
            view.Customize.Open("texturas");
            Dispatcher.UIThread.RunJobs();
            var texts = Texts(view.Customize).ToList();
            Assert.Contains("CHÃO", texts);
            Assert.Contains("Grama", texts);
            Assert.Contains("Casa (notas .md)", texts);
            Assert.Contains("Teste", texts);

            // back to "Nenhum": the original art returns
            await view.Customization.SetAsync(new JsonObject { ["cidade"] = new JsonObject { ["texturas"] = "" } });
            Assert.Null(view.World.Ground);
            Assert.Empty(view.Customization.BuildingArt);
            return true;
        }
        finally { ResetTheme(view); window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    private static double MagentaShare(Window window)
    {
        var frame = window.CaptureRenderedFrame()!;
        int w = frame.PixelSize.Width, h = frame.PixelSize.Height;
        var buffer = new byte[w * h * 4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { frame.CopyPixels(new Avalonia.PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buffer.Length, w * 4); }
        finally { handle.Free(); }
        int hit = 0;
        for (int i = 0; i < buffer.Length; i += 4)
        {
            int c0 = buffer[i], c1 = buffer[i + 1], c2 = buffer[i + 2];   // BGRA or RGBA: red and blue are both high, green low
            if (c0 > 120 && c2 > 120 && c1 < c0 * .55 && c1 < c2 * .55) hit++;
        }
        return hit / (double)(w * h);
    }

    [Fact]
    public async Task Customize_ThemesStylesAndPluginsPersistInTheVault() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var plugin = "urbe.plugin({ nome: 'Contador', versao: '1.2', descricao: 'Conta', ligar(){} });";
        var (view, window, dir, vault) = await VaultViewAsync(new() { ["Personalização/plugins/contador.js"] = plugin });
        try
        {
            var c = view.Customization;
            // Salvar como tema: current palette to Personalização/temas/<nome>.json, then it is a card in Aparência
            await c.SetAsync(new JsonObject { ["tema"] = "vinho", ["cores"] = new JsonObject { ["destaque"] = "#abcdef" } });
            var themePath = await c.SaveThemeAsync("Meu tema");
            Assert.Equal("Personalização/temas/Meu tema.json", themePath);
            var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(vault, "Personalização", "temas", "Meu tema.json")))!;
            Assert.Equal("vinho", (string)saved["base"]!);
            Assert.Equal("#abcdef", (string)saved["cores"]!["destaque"]!);
            Assert.Single(c.Themes());
            await c.SetAsync(new JsonObject { ["tema"] = themePath, ["cores"] = new JsonObject() });
            Assert.Equal("#abcdef", c.Palette.Colors["destaque"]);
            Assert.Equal("#abcdef", UrbeTheme.Current(UrbeTheme.Accent));
            view.Customize.Open("aparencia");
            Assert.Contains("Meu tema", Texts(view.Customize));

            // Estilos: a .css file listed, toggled in tema.json (it cannot style the native app)
            var css = "Personalização/estilos/meu.css";
            await c.UpsertAsync(css, "#renderedPreview h1 { color: red; }\n");
            Assert.Equal(new[] { css }, c.Styles().Select(s => s.Path));
            Assert.False(c.Styles()[0].On);
            await c.SetAsync(new JsonObject { ["estilos"] = new JsonObject { [css] = true }, ["css"] = "body{}" });
            Assert.True(c.Styles()[0].On);
            Assert.Contains("\"css\": \"body{}\"", File.ReadAllText(Path.Combine(vault, "Personalização", "tema.json")));

            // Plugins: listed from the code without running it; approval is bound to the code and kept per device
            var p = c.Plugins().Single();
            Assert.Equal(("Contador", "1.2", "desligado"), (p.Name, p.Version, p.Status));
            c.EnablePlugin(p.Path);
            Assert.Equal("aprovado", c.Plugins().Single().Status);
            c.SafeMode = true;
            Assert.Equal("seguro", c.Plugins().Single().Status);
            c.SafeMode = false;
            await c.UpsertAsync(p.Path, plugin + "\n// mudou");
            Assert.Equal("mudou", c.Plugins().Single().Status);
            c.EnablePlugin(p.Path);
            c.DisablePlugin(p.Path);
            Assert.Equal("desligado", c.Plugins().Single().Status);
            var made = await c.CreatePluginAsync("Diário d'água", "diario");
            Assert.Equal("Personalização/plugins/Diário d'água.js", made);
            Assert.Contains("nome: 'Diário d\\'água'", File.ReadAllText(Path.Combine(vault, "Personalização", "plugins", "Diário d'água.js")));
            Assert.Equal("desligado", c.Plugins().First(x => x.Path == made).Status);
            view.Customize.Open("plugins");
            Assert.Contains("Diário d\\", Texts(view.Customize));   // plugins.js reads the name up to the escaped quote
            return true;
        }
        finally { ResetTheme(view); window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Customize_PixelEditorDrawsFillsPicksAndProducesTheGridOfThePack() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, _) = await VaultViewAsync();
        try
        {
            var initial = Enumerable.Repeat("#112233", 16 * 16).ToArray();
            var task = view.Customize.EditPixelsAsync("Chão: Grama", 16, 16, initial, ["#112233", "#445566"]);
            var editor = view.Customize.Editor!;
            editor.Color_ = "#445566";
            editor.ApplyAt(1, 1, begin: true);                  // lápis
            editor.Tool = "balde"; editor.Color_ = "#778899";
            editor.ApplyAt(8, 8, begin: true);                  // balde: the rest of the 16×16
            Assert.Equal("#445566", editor.Pixels[1 * 16 + 1]);
            Assert.Equal("#778899", editor.Pixels[0]);
            editor.Tool = "conta"; editor.ApplyAt(1, 1, begin: true);
            Assert.Equal("#445566", editor.Color_);
            Assert.True(editor.Parent is not null);
            // Cancel returns null
            var fail = view.Customize.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == "Salvar");
            fail.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            var result = await task;
            Assert.NotNull(result);
            var grid = CustomizePanel.ToGrid(result!, 16, 16);
            Assert.Equal("{\"a\":\"#778899\",\"b\":\"#445566\"}", grid["cores"]!.ToJsonString());
            Assert.Equal("aaaaaaaaaaaaaaaa", (string)grid["pixels"]![0]!);
            Assert.Equal("abaaaaaaaaaaaaaa", (string)grid["pixels"]![1]!);
            Assert.Equal("..", (string)CustomizePanel.ToGrid(["", "", "", ""], 2, 2)["pixels"]![0]!);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
