using System.Text.RegularExpressions;
using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using AColor = Android.Graphics.Color;
using Path = System.IO.Path;

namespace Lunet.Android;

/// <summary>
/// Palette Studio (LUNET-499): the game's official colors in one file, used by the Sprite Studio, exported to code and read
/// by the AI. Generates shade ramps and harmonies, extracts colors from the project's art, imports/exports .gpl and hex.
/// </summary>
public sealed partial class MainActivity
{
    internal ProjectPalette? LoadProjectPalette()
    {
        if (_project is null) return null;
        var path = Path.Combine(_project.Directory, ProjectPalette.DefaultPath);
        if (!File.Exists(path)) return null;
        try { return ProjectPalette.Parse(File.ReadAllText(path)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            Toast.MakeText(this, "A paleta do projeto está inválida: " + ex.Message, ToastLength.Long)?.Show();
            return null;
        }
    }

    internal void SaveProjectPalette(ProjectPalette palette)
    {
        try { _project?.WriteText(ProjectPalette.DefaultPath, palette.ToJson()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível salvar a paleta: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void ShowPaletteStudio()
    {
        if (_project is null) return;
        SaveCurrent();
        var existing = LoadProjectPalette();
        if (existing is not null) { OpenPaletteStudio(existing); return; }
        var presets = ProjectPalette.Presets;
        var items = new[] { "Extrair das artes do projeto", "Começar vazia" }
            .Concat(presets.Select(p => "Inicial: " + p.Name)).ToArray();
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Criar a paleta do jogo")!
            .SetItems(items, (_, e) =>
            {
                ProjectPalette palette;
                if (e.Which == 0)
                {
                    palette = new ProjectPalette();
                    palette.AddRange(ProjectPalette.FromPixels(ProjectArtPixels(), 16));
                    if (palette.Colors.Count == 0) Toast.MakeText(this, "Ainda não há artes no projeto; adicione cores à mão.", ToastLength.Long)?.Show();
                }
                else if (e.Which == 1) palette = new ProjectPalette();
                else palette = ProjectPalette.FromPreset(presets[e.Which - 2].Id);
                SaveProjectPalette(palette);
                OpenPaletteStudio(palette);
            })!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .Show();
    }

    /// <summary>Pixels of the project's sprites and PNGs (bounded), for palette extraction.</summary>
    private IEnumerable<uint> ProjectArtPixels()
    {
        var project = _project!;
        int budget = 400_000;
        foreach (var file in project.ListFiles())
        {
            if (budget <= 0) yield break;
            uint[] pixels = [];
            try
            {
                if (file.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))
                    pixels = SpriteDocument.Parse(project.ReadText(file)).Frames.SelectMany(f => f).ToArray();
                else if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !file.StartsWith("Notes/", StringComparison.Ordinal))
                {
                    var absolute = Path.Combine(project.Directory, file);
                    if (File.Exists(absolute[..^4] + ".sprite.json")) continue; // the sheet of a sprite already counted
                    using var options = new BitmapFactory.Options { InSampleSize = 1 };
                    using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
                    BitmapFactory.DecodeFile(absolute, bounds)?.Dispose();
                    while (bounds.OutWidth / options.InSampleSize * (bounds.OutHeight / options.InSampleSize) > 65_536) options.InSampleSize *= 2;
                    using var bitmap = BitmapFactory.DecodeFile(absolute, options);
                    if (bitmap is null) continue;
                    var raw = new int[bitmap.Width * bitmap.Height];
                    bitmap.GetPixels(raw, 0, bitmap.Width, 0, 0, bitmap.Width, bitmap.Height);
                    pixels = raw.Select(p => unchecked((uint)p)).ToArray();
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException) { continue; }
            budget -= pixels.Length;
            foreach (var p in pixels) yield return p;
        }
    }

    private void OpenPaletteStudio(ProjectPalette palette)
    {
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var root = LunetUi.Column(this, 0);
        root.SetBackgroundColor(LunetUi.Background);

        var bar = LunetUi.Row(this, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.AddView(LunetUi.Icon(this, "‹", "Voltar", () => dialog.Dismiss()), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        var titles = LunetUi.Column(this, 0);
        var title = LunetUi.Label(this, palette.Name, LunetUi.TextLabel, LunetUi.Text, bold: true);
        var subtitle = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
        titles.AddView(title);
        titles.AddView(subtitle);
        titles.Clickable = true;
        bar.AddView(titles, LunetUi.Weight());
        root.AddView(bar);
        root.AddView(LunetUi.Divider(this));

        var grid = new GridLayout(this) { ColumnCount = LunetUi.IsExpanded(this) ? 8 : 4 };
        grid.SetPadding(Dp(10), Dp(10), Dp(10), Dp(10));
        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.AddView(grid);
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        int selected = -1;
        void Changed()
        {
            SaveProjectPalette(palette);
            Render();
        }

        void Render()
        {
            title.Text = palette.Name;
            subtitle.Text = palette.Colors.Count == 0
                ? "Sem cores. Toque em ＋ Cor ou gere uma rampa."
                : $"{palette.Colors.Count} cores · {ProjectPalette.DefaultPath} · toque para editar, segure para mais";
            grid.RemoveAllViews();
            for (int i = 0; i < palette.Colors.Count; i++)
            {
                int index = i;
                var swatch = palette.Colors[i];
                var cell = LunetUi.Column(this, 2);
                cell.SetPadding(Dp(4), Dp(4), Dp(4), Dp(4));
                cell.Clickable = true;
                cell.Background = LunetUi.Pressable(this, index == selected ? LunetUi.AccentSoft : LunetUi.Surface, LunetUi.RadiusSmall,
                    index == selected ? LunetUi.Accent : LunetUi.Border);
                var chip = new View(this) { Background = LunetUi.Shape(this, new AColor(unchecked((int)swatch.Argb)), LunetUi.RadiusSmall, LunetUi.Border) };
                cell.AddView(chip, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(48)));
                var name = LunetUi.Label(this, swatch.Name, LunetUi.TextCaption, LunetUi.Text, bold: true);
                name.SetSingleLine(true);
                name.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
                cell.AddView(name);
                cell.AddView(LunetUi.Label(this, SpriteColor.ToHex(swatch.Argb), 11, LunetUi.TextMuted));
                cell.ContentDescription = swatch.Name + " " + SpriteColor.ToHex(swatch.Argb);
                cell.Click += (_, _) => { selected = index; EditSwatch(palette, index, Changed); };
                cell.LongClick += (_, e) => { selected = index; Render(); SwatchMenu(palette, index, Changed); e.Handled = true; };
                var layout = new GridLayout.LayoutParams(GridLayout.InvokeSpec(GridLayout.Undefined, 1f), GridLayout.InvokeSpec(GridLayout.Undefined, 1f)) { Width = 0 };
                layout.SetMargins(Dp(4), Dp(4), Dp(4), Dp(4));
                grid.AddView(cell, layout);
            }
        }

        titles.Click += (_, _) =>
        {
            var input = LunetUi.Input(this, "Nome da paleta", palette.Name);
            new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                .SetTitle("Nome da paleta")!.SetView(Padded(input))!
                .SetPositiveButton("OK", (_, _) => { if ((input.Text ?? "").Trim() is { Length: > 0 } n) { palette.Name = n; Changed(); } })!
                .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
        };

        var actions = LunetUi.Row(this, 6);
        actions.SetPadding(Dp(10), Dp(8), Dp(10), Dp(10));
        void Action(string label, Action run) => actions.AddView(LunetUi.Button(this, label, run, ButtonStyle.Secondary));
        uint Base() => selected >= 0 && selected < palette.Colors.Count ? palette.Colors[selected].Argb
            : palette.Colors.Count > 0 ? palette.Colors[^1].Argb : 0xff4f62a8;
        Action("＋ Cor", () =>
        {
            palette.AddRange([Base() == 0xff4f62a8 && palette.Colors.Count == 0 ? 0xff4f62a8 : SpriteColor.FromHsv(SpriteColor.ToHsv(Base()).Hue + 25, 0.6f, 0.75f, 255)]);
            selected = palette.Colors.Count - 1;
            Changed();
            EditSwatch(palette, selected, Changed);
        });
        Action("Rampa de tons", () => ChooseNumber("Rampa de tons a partir da cor selecionada", [3, 4, 5, 6, 7], steps =>
        {
            int added = palette.AddRange(ProjectPalette.Ramp(Base(), steps), ProjectPalette.HueName(Base()));
            Toast.MakeText(this, $"{added} tom(ns) adicionados (sombras mais frias, luzes mais quentes).", ToastLength.Short)?.Show();
            Changed();
        }));
        Action("Harmonia", () =>
        {
            var kinds = new (PaletteHarmony Kind, string Label)[]
            {
                (PaletteHarmony.Complementary, "Complementar (contraste forte)"), (PaletteHarmony.Analogous, "Análoga (vizinhas, calma)"),
                (PaletteHarmony.Triadic, "Tríade (equilibrada, viva)"), (PaletteHarmony.SplitComplementary, "Complementar dividida"),
                (PaletteHarmony.Tetradic, "Tétrade (quatro famílias)"), (PaletteHarmony.Monochrome, "Monocromática (um matiz)"),
            };
            new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                .SetTitle("Harmonia a partir da cor selecionada")!
                .SetItems(kinds.Select(k => k.Label).ToArray(), (_, e) =>
                {
                    palette.AddRange(ProjectPalette.Harmony(Base(), kinds[e.Which].Kind));
                    Changed();
                })!.Show();
        });
        Action("Das artes do projeto", () => ChooseNumber("Quantas cores principais extrair?", [4, 8, 12, 16, 24], count =>
        {
            int added = palette.AddRange(ProjectPalette.FromPixels(ProjectArtPixels(), count));
            Toast.MakeText(this, added == 0 ? "Nenhuma cor nova encontrada." : $"{added} cor(es) adicionadas.", ToastLength.Short)?.Show();
            Changed();
        }));
        Action("Código C#", () =>
        {
            var game = _project!.ReadText(_project.Manifest.EntryPoint);
            var ns = Regex.Match(game, @"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : null;
            const string target = "Data/GamePalette.cs";
            _project.WriteText(target, palette.ToCSharp(ns));
            RefreshExplorer();
            new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                .SetTitle("Paleta no código")!
                .SetMessage($"Gerado {target}. Use as mesmas cores no jogo:\n\n" +
                            $"GraphicsDevice.Clear(GamePalette.{palette.ExportNames().FirstOrDefault() ?? "Cor"});\n" +
                            "batch.DrawString(fonte, \"Olá\", posição, GamePalette.All[1]);\n\n" +
                            "Exporte de novo sempre que mudar a paleta.")!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
        });
        Action("Contraste", () => ShowContrast(palette));
        Action("Importar .gpl / hex", () =>
        {
            var files = _project!.ListFiles().Where(f => f.EndsWith(".gpl", StringComparison.OrdinalIgnoreCase) ||
                                                         f.EndsWith(".hex", StringComparison.OrdinalIgnoreCase) ||
                                                         f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (files.Length == 0) { Toast.MakeText(this, "Importe um .gpl ou .hex para Notes/ pelo Explorer.", ToastLength.Long)?.Show(); return; }
            new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                .SetTitle("Importar paleta")!
                .SetItems(files, (_, e) =>
                {
                    var imported = ProjectPalette.Import(_project.ReadText(files[e.Which]), Path.GetFileNameWithoutExtension(files[e.Which]));
                    int added = palette.AddRange(imported.Argb);
                    Toast.MakeText(this, $"{added} cor(es) importadas.", ToastLength.Short)?.Show();
                    Changed();
                })!.Show();
        });
        Action("Exportar .gpl", () =>
        {
            var target = "Notes/" + ProjectPalette.Identifier(palette.Name) + ".gpl";
            _project!.WriteText(target, palette.ToGpl());
            RefreshExplorer();
            Toast.MakeText(this, "Exportado " + target + " (GIMP, Krita, Aseprite).", ToastLength.Long)?.Show();
        });
        var actionScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        actionScroll.AddView(actions);
        root.AddView(LunetUi.Divider(this));
        root.AddView(actionScroll);

        Render();
        dialog.SetContentView(root);
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }

    private View Padded(View view)
    {
        var frame = new FrameLayout(this);
        frame.SetPadding(Dp(20), Dp(8), Dp(20), Dp(4));
        frame.AddView(view);
        return frame;
    }

    private void ChooseNumber(string title, int[] options, Action<int> choose) =>
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle(title)!
            .SetItems(options.Select(o => o.ToString()).ToArray(), (_, e) => choose(options[e.Which]))!
            .Show();

    private void SwatchMenu(ProjectPalette palette, int index, Action changed)
    {
        var items = new (string Label, Action Run)[]
        {
            ("Editar", () => EditSwatch(palette, index, changed)),
            ("Mover para a esquerda", () => { if (index > 0) { (palette.Colors[index - 1], palette.Colors[index]) = (palette.Colors[index], palette.Colors[index - 1]); changed(); } }),
            ("Mover para a direita", () => { if (index < palette.Colors.Count - 1) { (palette.Colors[index + 1], palette.Colors[index]) = (palette.Colors[index], palette.Colors[index + 1]); changed(); } }),
            ("Duplicar", () => { palette.Colors.Insert(index + 1, palette.Colors[index] with { Name = palette.UniqueName(palette.Colors[index].Name) }); changed(); }),
            ("Remover", () => { palette.Colors.RemoveAt(index); changed(); }),
        };
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle(palette.Colors[index].Name)!
            .SetItems(items.Select(i => i.Label).ToArray(), (_, e) => items[e.Which].Run())!.Show();
    }

    private void EditSwatch(ProjectPalette palette, int index, Action changed)
    {
        var swatch = palette.Colors[index];
        var (h, s, v, a) = SpriteColor.ToHsv(swatch.Argb);
        var form = LunetUi.Column(this, 8);
        form.SetPadding(Dp(20), Dp(10), Dp(20), Dp(6));
        var preview = new View(this);
        form.AddView(preview, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(56)));
        var name = LunetUi.Input(this, "Nome (vira o nome no código)", swatch.Name);
        form.AddView(name);
        var hex = LunetUi.Input(this, "#RRGGBB ou #AARRGGBB", SpriteColor.ToHex(swatch.Argb));
        bool syncing = false;
        uint Current() => SpriteColor.FromHsv(h, s, v, a);
        void Paint()
        {
            preview.Background = LunetUi.Shape(this, new AColor(unchecked((int)Current())), LunetUi.RadiusSmall, LunetUi.Border);
            syncing = true;
            hex.Text = SpriteColor.ToHex(Current());
            syncing = false;
        }
        SeekBar Slider(string label, int max, int value, Action<int> set)
        {
            form.AddView(LunetUi.Label(this, label, LunetUi.TextCaption, LunetUi.TextMuted));
            var bar = new SeekBar(this) { Max = max, Progress = value };
            bar.ProgressChanged += (_, e) => { if (e.FromUser) { set(e.Progress); Paint(); } };
            form.AddView(bar);
            return bar;
        }
        var hue = Slider("Matiz", 360, (int)h, x => h = x);
        var sat = Slider("Saturação", 100, (int)(s * 100), x => s = x / 100f);
        var val = Slider("Brilho", 100, (int)(v * 100), x => v = x / 100f);
        var alpha = Slider("Opacidade", 255, a, x => a = x);
        form.AddView(hex);
        hex.TextChanged += (_, _) =>
        {
            if (syncing || SpriteColor.ParseHex(hex.Text) is not { } parsed) return;
            (h, s, v, a) = SpriteColor.ToHsv(parsed);
            hue.Progress = (int)h; sat.Progress = (int)(s * 100); val.Progress = (int)(v * 100); alpha.Progress = a;
            preview.Background = LunetUi.Shape(this, new AColor(unchecked((int)parsed)), LunetUi.RadiusSmall, LunetUi.Border);
        };
        Paint();
        var scroll = new ScrollView(this);
        scroll.AddView(form);
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Cor da paleta")!
            .SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetNeutralButton("Remover", (_, _) => { palette.Colors.RemoveAt(index); changed(); })!
            .SetPositiveButton("Salvar", (_, _) =>
            {
                var label = (name.Text ?? "").Trim();
                palette.Colors[index] = new PaletteSwatch(label.Length == 0 ? ProjectPalette.HueName(Current()) : label,
                    SpriteColor.ParseHex(hex.Text) ?? Current());
                changed();
            })!
            .Show();
    }

    /// <summary>Text legibility between palette colors (WCAG): which pairs work for UI text and HUD.</summary>
    private void ShowContrast(ProjectPalette palette)
    {
        if (palette.Colors.Count < 2) { Toast.MakeText(this, "Adicione pelo menos duas cores.", ToastLength.Short)?.Show(); return; }
        var pairs = new List<(PaletteSwatch Text, PaletteSwatch Back, double Ratio)>();
        for (int i = 0; i < palette.Colors.Count; i++)
            for (int j = i + 1; j < palette.Colors.Count; j++)
                pairs.Add((palette.Colors[i], palette.Colors[j], ProjectPalette.Contrast(palette.Colors[i].Argb, palette.Colors[j].Argb)));
        var list = LunetUi.Column(this, 6);
        list.SetPadding(Dp(16), Dp(8), Dp(16), Dp(8));
        list.AddView(LunetUi.Label(this, "Pares para texto e HUD (4,5:1 ou mais é legível; 3:1 serve para títulos grandes).",
            LunetUi.TextCaption, LunetUi.TextMuted));
        foreach (var (text, back, ratio) in pairs.OrderByDescending(p => p.Ratio).Take(24))
        {
            var row = LunetUi.Label(this, $"  {text.Name} sobre {back.Name}  ·  {ratio:0.0}:1 {(ratio >= 4.5 ? "✓" : ratio >= 3 ? "títulos" : "✕")}",
                LunetUi.TextBody, new AColor(unchecked((int)(text.Argb | 0xff000000))));
            row.SetPadding(Dp(10), Dp(10), Dp(10), Dp(10));
            row.Background = LunetUi.Shape(this, new AColor(unchecked((int)(back.Argb | 0xff000000))), LunetUi.RadiusSmall);
            list.AddView(row, LunetUi.FillWidth());
        }
        var scroll = new ScrollView(this);
        scroll.AddView(list);
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Contraste da paleta")!.SetView(scroll)!.SetPositiveButton("OK", (_, _) => { })!.Show();
    }
}
