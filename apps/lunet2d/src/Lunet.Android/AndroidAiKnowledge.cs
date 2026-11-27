using Android.Content.Res;
using Lunet.Ai;

namespace Lunet.Android;

/// <summary>The AI's knowledge of Lunet: the Framework API reference and guides shipped in the APK (offline).</summary>
internal static class AndroidAiKnowledge
{
    public static LunetAiKnowledge Load(AssetManager assets)
    {
        string Read(string path)
        {
            using var stream = assets.Open(path);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        string? api = null;
        try { api = Read("docs/lunet-framework.json"); } catch (Java.IO.IOException) { }
        var guides = new Dictionary<string, string>();
        foreach (var file in assets.List("docs/guides") ?? [])
            if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                guides[System.IO.Path.GetFileNameWithoutExtension(file)] = Read("docs/guides/" + file);
        return LunetAiKnowledge.Create(api, guides);
    }
}
