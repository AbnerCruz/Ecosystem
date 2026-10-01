namespace Lunet.Compiler;

/// <summary>Um arquivo C# a compilar. <see cref="Path"/> é relativo ao projeto e só serve para diagnósticos.</summary>
public sealed record SourceFile(string Path, string Text);
