using Microsoft.CodeAnalysis;

namespace Lunet.Compiler;

/// <summary>Fornece os assemblies de referência contra os quais o código do jogo é compilado.</summary>
public interface IReferenceProvider
{
    IReadOnlyList<MetadataReference> GetReferences();
}
