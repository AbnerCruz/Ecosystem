using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;

namespace Lunet.Compiler;

/// <summary>
/// Referências obtidas dos assemblies já carregados no processo, lendo seus metadados em memória.
/// Não usa caminhos de arquivo, então funciona no Android (assemblies dentro do APK) e offline.
/// Somente a API permitida aos jogos é exposta: BCL (<c>System.*</c>, <c>netstandard</c>) e <c>Lunet.Framework</c>.
/// </summary>
public sealed unsafe class LoadedAssembliesReferenceProvider : IReferenceProvider
{
    private static readonly string[] RequiredAssemblies =
    [
        "System.Private.CoreLib", "System.Runtime", "System.Collections", "System.Linq",
        "System.Numerics.Vectors", "System.Runtime.Numerics", "System.Memory", "System.Console",
        "System.Text.Json", "System.Threading", "System.Diagnostics.Debug", "System.Runtime.Extensions",
        "System.Collections.Concurrent", "System.Text.RegularExpressions", "System.Runtime.InteropServices",
        "netstandard",
    ];

    private readonly Assembly[] _extra;
    private IReadOnlyList<MetadataReference>? _cached;

    /// <param name="extraAssemblies">Assemblies adicionais expostos ao jogo (ex.: o do Lunet.Framework).</param>
    public LoadedAssembliesReferenceProvider(params Assembly[] extraAssemblies) => _extra = extraAssemblies;

    public IReadOnlyList<MetadataReference> GetReferences() => _cached ??= Build();

    private IReadOnlyList<MetadataReference> Build()
    {
        var byName = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var name in RequiredAssemblies)
        {
            try { byName[name] = Assembly.Load(new AssemblyName(name)); }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException) { }
        }
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var simple = assembly.GetName().Name;
            if (simple is not null && (simple.StartsWith("System.", StringComparison.Ordinal) || simple == "netstandard"))
                byName.TryAdd(simple, assembly);
        }
        foreach (var assembly in _extra)
            byName[assembly.GetName().Name ?? assembly.FullName ?? "extra"] = assembly;

        var references = new List<MetadataReference>(byName.Count);
        foreach (var assembly in byName.Values)
        {
            if (assembly.IsDynamic || !assembly.TryGetRawMetadata(out var blob, out var length)) continue;
            var module = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
            references.Add(AssemblyMetadata.Create(module).GetReference(display: assembly.GetName().Name));
        }
        return references;
    }
}
