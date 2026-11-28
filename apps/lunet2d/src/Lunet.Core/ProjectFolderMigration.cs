namespace Lunet.Core;

/// <summary>Installs verified project folders without changing or deleting the source.</summary>
public static class ProjectFolderMigration
{
    public static void Migrate(ProjectStore source, ProjectStore target)
    {
        foreach (var name in source.List())
        {
            var game = source.Open(name);
            if (target.Root.DirectoryExists(name))
            {
                if (ContentsMatch(game.Files, target.Root.Sub(name), "")) continue;
                throw new IOException($"A pasta escolhida já contém outra versão de {name}.");
            }
            if (target.FindIdentityConflict(game.Manifest) is { } duplicate)
                throw new IOException($"O jogo {name} já existe na pasta escolhida como {duplicate}.");
            Install(game.Files, target.Root, name);
        }
    }

    internal static void Install(IProjectFiles source, IProjectFiles target, string name)
    {
        var stage = ".lunet-migrate-" + Guid.NewGuid().ToString("N");
        target.CreateDirectory(stage);
        try
        {
            var staged = target.Sub(stage);
            Copy(source, staged);
            if (!ContentsMatch(source, staged, ""))
                throw new IOException($"A cópia de {name} não conferiu. A origem foi preservada.");
            target.Move(stage, name);
        }
        finally
        {
            if (target.DirectoryExists(stage)) target.Delete(stage);
        }
    }

    internal static void Copy(IProjectFiles source, IProjectFiles target, string folder = "")
    {
        foreach (var entry in source.List(folder))
        {
            var path = Combine(folder, entry.Name);
            if (source.LocalDirectory is { } disk &&
                (File.GetAttributes(Path.Combine(disk, path)) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A migração não segue links fora da pasta do jogo: " + path);
            if (entry.IsDirectory)
            {
                target.CreateDirectory(path);
                Copy(source, target, path);
            }
            else target.Write(path, source.Read(path));
        }
    }

    private static bool ContentsMatch(IProjectFiles source, IProjectFiles target, string folder)
    {
        var entries = source.List(folder);
        if (!entries.ToHashSet().SetEquals(target.List(folder))) return false;
        foreach (var entry in entries)
        {
            var path = Combine(folder, entry.Name);
            if (source.LocalDirectory is { } disk &&
                (File.GetAttributes(Path.Combine(disk, path)) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A migração não segue links fora da pasta do jogo: " + path);
            if (entry.IsDirectory)
            {
                if (!ContentsMatch(source, target, path)) return false;
            }
            else if (!source.Read(path).AsSpan().SequenceEqual(target.Read(path))) return false;
        }
        return true;
    }

    private static string Combine(string folder, string name)
    {
        if (name is "." or ".." || name.IndexOfAny(['/', '\\']) >= 0)
            throw new IOException("Nome de arquivo inválido na pasta de projetos.");
        return folder.Length == 0 ? name : folder + "/" + name;
    }
}
