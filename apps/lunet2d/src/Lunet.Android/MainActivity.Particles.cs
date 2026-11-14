using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Studio;
using Path = System.IO.Path;

namespace Lunet.Android;

/// <summary>Particle Studio entry points (LUNET-504). Effects live in <c>Content/Effects/*.particles.json</c>.</summary>
public sealed partial class MainActivity
{
    private const string EffectsFolder = "Content/Effects";
    private const string ParticleExtension = ".particles.json";

    private void ShowParticleStudio()
    {
        if (_project is null) return;
        SaveCurrent();
        var effects = _project.ListFiles().Where(f => f.EndsWith(ParticleExtension, StringComparison.OrdinalIgnoreCase)).ToArray();
        var items = new[] { "+ Novo efeito" }.Concat(effects).ToArray();
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Particle Studio")!
            .SetItems(items, (_, e) =>
            {
                if (e.Which == 0)
                {
                    var presets = ParticlePresets.All;
                    new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                        .SetTitle("Começar de")!
                        .SetItems(presets.Select(p => p.Name).ToArray(), (_, p) =>
                        {
                            var preset = presets[p.Which];
                            var name = UniqueEffectName(preset.Id);
                            OpenParticleStudio(EffectsFolder + "/" + name + ParticleExtension, ParticleDraft.From(preset));
                        })!.Show();
                    return;
                }
                var path = effects[e.Which - 1];
                try
                {
                    var json = _project.ReadText(path);
                    var draft = ParticleDraft.From(ParticleSettings.FromJson(json));
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("EmissionRate", out var rate)) draft.EmissionRate = rate.GetSingle();
                    if (doc.RootElement.TryGetProperty("MaxParticles", out var max)) draft.MaxParticles = max.GetInt32();
                    OpenParticleStudio(path, draft);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or ProjectException)
                {
                    Toast.MakeText(this, "Não foi possível abrir o efeito: " + ex.Message, ToastLength.Long)?.Show();
                }
            })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }

    private string UniqueEffectName(string baseName)
    {
        var name = baseName;
        for (int i = 2; File.Exists(Path.Combine(_project!.Directory, EffectsFolder, name + ParticleExtension)); i++) name = baseName + "-" + i;
        return name;
    }

    private void OpenParticleStudio(string path, ParticleDraft draft)
    {
        var project = _project!;
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var name = Path.GetFileName(path)[..^ParticleExtension.Length];
        var view = new ParticleStudioView(this, draft, name, current =>
        {
            try
            {
                project.WriteText(path, current.ToJson());
                RefreshExplorer();
                var load = path["Content/".Length..];
                new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                    .SetTitle("Efeito salvo")!
                    .SetMessage($"{path}\n\nNo LoadContent:\n" +
                                $"_efeito = Content.LoadParticleEmitter(\"{load}\", Texture2D.CreateCircle(GraphicsDevice, 16, Color.White));\n" +
                                (current.EmissionRate > 0 ? "_efeito.Start();\n" : "") +
                                "\nNo Update:\n_efeito.Position = posicao;\n" + (current.Burst > 0 ? $"_efeito.Burst({current.Burst}, posicao); // quando acontecer\n" : "") +
                                "_efeito.Update(time.DeltaSeconds);\n\nNo Draw:\n" +
                                (current.Additive ? "batch.Begin(BlendState.Additive);\n" : "batch.Begin();\n") + "_efeito.Draw(batch);\nbatch.End();")!
                    .SetPositiveButton("OK", (_, _) => { })!.Show();
            }
            catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException or ProjectException or UnauthorizedAccessException)
            {
                Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();
            }
        }, () => dialog.Dismiss());
        dialog.SetContentView(view);
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }
}
