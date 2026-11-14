using System.Text.RegularExpressions;
using Lunet.Core;

namespace Lunet.Ai;

/// <summary>
/// Checks static texture references when the agent compiles a project.
/// Dynamic references and actual game execution still require Run/Preview validation.
/// </summary>
public static class LunetAiAssetValidation
{
    private static readonly Regex TextureLiteral = new(
        @"\bContent\s*\.\s*LoadTexture\s*\(\s*\x22(?<path>[^\x22]+)\x22",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Validate(LunetProject project)
    {
        var errors = new List<string>();
        foreach (var source in project.LoadSources())
        {
            foreach (Match match in TextureLiteral.Matches(source.Text))
            {
                var path = match.Groups["path"].Value.Replace('\\', '/');
                var name = path.StartsWith("Content/", StringComparison.Ordinal)
                    ? path["Content/".Length..] : path;
                if (name.Length == 0 || name.StartsWith("/", StringComparison.Ordinal) ||
                    name.Split('/').Any(part => part is "" or "." or ".."))
                {
                    errors.Add(source.Path + ": caminho de textura inválido: " + path);
                    continue;
                }

                var candidates = new List<string> { "Content/" + name };
                if (name.StartsWith("Assets/", StringComparison.Ordinal))
                    candidates.Add(name);
                var located = candidates.FirstOrDefault(candidate =>
                    File.Exists(Path.Combine(project.Directory, candidate.Replace('/', Path.DirectorySeparatorChar))));
                if (located is null)
                {
                    errors.Add(source.Path + ": textura ausente: " + path +
                        " (procurado: " + string.Join(" ou ", candidates) + ")");
                    continue;
                }

                if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = File.OpenRead(Path.Combine(project.Directory,
                        located.Replace('/', Path.DirectorySeparatorChar)));
                    Span<byte> signature = stackalloc byte[8];
                    if (stream.Read(signature) != signature.Length ||
                        !signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                        errors.Add(source.Path + ": o arquivo não tem assinatura PNG: " + located);
                }
            }
        }
        return errors;
    }
}
