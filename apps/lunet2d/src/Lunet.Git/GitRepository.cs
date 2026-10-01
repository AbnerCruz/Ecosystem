using System.Text;
using System.Text.RegularExpressions;

namespace Lunet.Git;

/// <summary>Mudança de um arquivo entre duas versões.</summary>
public enum FileChange { None, Added, Modified, Deleted, Untracked }

/// <summary>Situação de um arquivo: <see cref="Staged"/> (preparado, HEAD × índice) e <see cref="Unstaged"/> (diretório de trabalho × índice).</summary>
public sealed record StatusEntry(string Path, FileChange Staged, FileChange Unstaged);

public sealed record BranchInfo(string Name, ObjectId Id, bool IsCurrent, bool IsRemote);

public sealed record FileDiffInfo(string Path, FileChange Change);

public enum MergeOutcome { UpToDate, FastForward, Merged }

/// <summary>
/// Repositório Git implementado em C# puro (funciona no Android sem bibliotecas nativas). Lê e grava o formato padrão do Git,
/// então o mesmo repositório pode ser aberto por qualquer outro cliente Git.
/// </summary>
public sealed partial class GitRepository
{
    private static readonly Regex BranchName = new(@"^(?!-)(?!.*(\.\.|//|@\{|\.lock$|/$|^/))[A-Za-z0-9._/\-]+$", RegexOptions.CultureInvariant);

    private GitRepository(string workDirectory)
    {
        WorkDirectory = Path.GetFullPath(workDirectory);
        GitDirectory = Path.Combine(WorkDirectory, ".git");
        Objects = new ObjectStore(Path.Combine(GitDirectory, "objects"));
    }

    public string WorkDirectory { get; }
    public string GitDirectory { get; }
    public ObjectStore Objects { get; }

    public static bool IsRepository(string directory) => File.Exists(Path.Combine(directory, ".git", "HEAD"));

    public static GitRepository Init(string directory, string initialBranch = "main")
    {
        var repository = new GitRepository(directory);
        if (IsRepository(directory)) throw new GitException("Este projeto já é um repositório Git.");
        Directory.CreateDirectory(Path.Combine(repository.GitDirectory, "objects"));
        Directory.CreateDirectory(Path.Combine(repository.GitDirectory, "refs", "heads"));
        Directory.CreateDirectory(Path.Combine(repository.GitDirectory, "refs", "tags"));
        File.WriteAllText(Path.Combine(repository.GitDirectory, "HEAD"), $"ref: refs/heads/{initialBranch}\n");
        var config = new GitConfig(Path.Combine(repository.GitDirectory, "config"));
        config.Set("core", null, "repositoryformatversion", "0");
        config.Set("core", null, "filemode", "false");
        config.Set("core", null, "bare", "false");
        config.Save();
        return repository;
    }

    public static GitRepository Open(string directory)
    {
        if (!IsRepository(directory)) throw new GitException("Este projeto ainda não é um repositório Git.");
        return new GitRepository(directory);
    }

    public GitConfig Config => new(Path.Combine(GitDirectory, "config"));

    // ---------- Referências ----------

    /// <summary>Nome do ramo atual, ou nulo com HEAD destacado.</summary>
    public string? CurrentBranch
    {
        get
        {
            var head = File.ReadAllText(Path.Combine(GitDirectory, "HEAD")).Trim();
            return head.StartsWith("ref: refs/heads/", StringComparison.Ordinal) ? head["ref: refs/heads/".Length..] : null;
        }
    }

    public ObjectId? Head
    {
        get
        {
            var head = File.ReadAllText(Path.Combine(GitDirectory, "HEAD")).Trim();
            return head.StartsWith("ref: ", StringComparison.Ordinal) ? ResolveRef(head[5..]) : ObjectId.TryParse(head, out var id) ? id : null;
        }
    }

    public ObjectId? ResolveRef(string reference)
    {
        var path = Path.Combine(GitDirectory, reference.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            var content = File.ReadAllText(path).Trim();
            if (content.StartsWith("ref: ", StringComparison.Ordinal)) return ResolveRef(content[5..]);
            return ObjectId.TryParse(content, out var id) ? id : null;
        }
        return PackedRefs().TryGetValue(reference, out var packed) ? packed : null;
    }

    private Dictionary<string, ObjectId> PackedRefs()
    {
        var result = new Dictionary<string, ObjectId>(StringComparer.Ordinal);
        var path = Path.Combine(GitDirectory, "packed-refs");
        if (!File.Exists(path)) return result;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0 || line[0] is '#' or '^') continue;
            var space = line.IndexOf(' ');
            if (space == 40 && ObjectId.TryParse(line.AsSpan(0, 40), out var id)) result[line[(space + 1)..]] = id;
        }
        return result;
    }

    public void UpdateRef(string reference, ObjectId id)
    {
        var path = Path.Combine(GitDirectory, reference.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".lock";
        File.WriteAllText(temporary, id.Hex + "\n");
        File.Move(temporary, path, overwrite: true);
    }

    private void DeleteRef(string reference)
    {
        var path = Path.Combine(GitDirectory, reference.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path)) File.Delete(path);
        var packed = Path.Combine(GitDirectory, "packed-refs");
        if (File.Exists(packed))
        {
            var lines = File.ReadAllLines(packed).Where(l => !l.EndsWith(" " + reference, StringComparison.Ordinal)).ToArray();
            File.WriteAllLines(packed, lines);
        }
    }

    private void SetHeadToBranch(string branch) => File.WriteAllText(Path.Combine(GitDirectory, "HEAD"), $"ref: refs/heads/{branch}\n");

    private void SetHeadDetached(ObjectId id) => File.WriteAllText(Path.Combine(GitDirectory, "HEAD"), id.Hex + "\n");

    /// <summary>Move o ramo atual (ou o HEAD destacado) para o commit dado.</summary>
    private void MoveHead(ObjectId id)
    {
        if (CurrentBranch is { } branch) UpdateRef("refs/heads/" + branch, id);
        else SetHeadDetached(id);
    }

    public IReadOnlyList<BranchInfo> Branches(bool includeRemote = false)
    {
        var refs = new Dictionary<string, ObjectId>(PackedRefs(), StringComparer.Ordinal);
        void Collect(string prefix)
        {
            var root = Path.Combine(GitDirectory, prefix.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root)) return;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".lock", StringComparison.Ordinal)) continue;
                var name = prefix + "/" + Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                if (ResolveRef(name) is { } id) refs[name] = id;
            }
        }
        Collect("refs/heads");
        if (includeRemote) Collect("refs/remotes");
        var current = CurrentBranch;
        return refs
            .Where(kv => kv.Key.StartsWith("refs/heads/", StringComparison.Ordinal) || includeRemote && kv.Key.StartsWith("refs/remotes/", StringComparison.Ordinal) && !kv.Key.EndsWith("/HEAD", StringComparison.Ordinal))
            .Select(kv => kv.Key.StartsWith("refs/heads/", StringComparison.Ordinal)
                ? new BranchInfo(kv.Key["refs/heads/".Length..], kv.Value, kv.Key["refs/heads/".Length..] == current, false)
                : new BranchInfo(kv.Key["refs/remotes/".Length..], kv.Value, false, true))
            .OrderBy(b => b.IsRemote).ThenBy(b => b.Name, StringComparer.Ordinal)
            .ToList();
    }

    public void CreateBranch(string name, ObjectId? start = null)
    {
        ValidateBranchName(name);
        if (ResolveRef("refs/heads/" + name) is not null) throw new GitException($"O ramo \"{name}\" já existe.");
        var target = start ?? Head ?? throw new GitException("Faça o primeiro commit antes de criar ramos.");
        UpdateRef("refs/heads/" + name, target);
    }

    public void DeleteBranch(string name, bool force = false)
    {
        if (name == CurrentBranch) throw new GitException("Não é possível excluir o ramo atual. Troque de ramo antes.");
        var tip = ResolveRef("refs/heads/" + name) ?? throw new GitException($"O ramo \"{name}\" não existe.");
        if (!force && Head is { } head && !IsAncestor(tip, head))
            throw new GitException($"O ramo \"{name}\" tem commits que não foram mesclados. Exclua com força se tiver certeza.");
        DeleteRef("refs/heads/" + name);
    }

    private static void ValidateBranchName(string name)
    {
        if (!BranchName.IsMatch(name)) throw new GitException($"\"{name}\" não é um nome de ramo válido (sem espaços, \"..\" ou terminar com \"/\").");
    }

    // ---------- Objetos: árvores e commits ----------

    public CommitInfo GetCommit(ObjectId id)
    {
        var obj = Objects.Read(id);
        if (obj.Type != ObjectType.Commit) throw new GitException($"{id.Short} não é um commit.");
        return CommitInfo.Parse(id, obj.Data);
    }

    public IReadOnlyDictionary<string, (string Mode, ObjectId Id)> Flatten(ObjectId? tree)
    {
        var result = new Dictionary<string, (string, ObjectId)>(StringComparer.Ordinal);
        if (tree is { } root) FlattenInto(root, "", result);
        return result;
    }

    private void FlattenInto(ObjectId treeId, string prefix, Dictionary<string, (string, ObjectId)> result)
    {
        var tree = Objects.Read(treeId);
        if (tree.Type != ObjectType.Tree) throw new GitException("Objeto esperado como árvore.");
        foreach (var entry in TreeFormat.Parse(tree.Data))
        {
            if (entry.IsTree) FlattenInto(entry.Id, prefix + entry.Name + "/", result);
            else if (entry.Mode != "160000") result[prefix + entry.Name] = (entry.Mode, entry.Id);
        }
    }

    private ObjectId? HeadTree() => Head is { } head ? GetCommit(head).Tree : null;

    private ObjectId WriteTree(GitIndex index)
    {
        var root = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var entry in index.Entries)
        {
            var parts = entry.Path.Split('/');
            var node = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (!node.TryGetValue(parts[i], out var child)) node[parts[i]] = child = new SortedDictionary<string, object>(StringComparer.Ordinal);
                node = (SortedDictionary<string, object>)child;
            }
            node[parts[^1]] = entry;
        }
        return WriteTreeNode(root);
    }

    private ObjectId WriteTreeNode(SortedDictionary<string, object> node)
    {
        var entries = new List<TreeEntry>();
        foreach (var (name, child) in node)
        {
            if (child is IndexEntry file) entries.Add(new TreeEntry(file.Mode, name, file.Id));
            else entries.Add(new TreeEntry("40000", name, WriteTreeNode((SortedDictionary<string, object>)child)));
        }
        return Objects.Write(ObjectType.Tree, TreeFormat.Serialize(entries));
    }

    /// <summary>Caminho absoluto de um arquivo do projeto, recusando fugas da pasta (".." ou absolutos).</summary>
    private string WorkPath(string relative)
    {
        if (relative.Length == 0 || relative.StartsWith('/') || relative.Split('/').Any(p => p is ".." or ".git" or ""))
            throw new GitException($"Caminho inseguro no repositório: \"{relative}\".");
        return Path.Combine(WorkDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private GitIndex LoadIndex() => GitIndex.Load(Path.Combine(GitDirectory, "index"));

    private void SaveIndex(GitIndex index) => index.Save(Path.Combine(GitDirectory, "index"));

    // ---------- Histórico ----------

    /// <summary>Commits do mais novo para o mais antigo (por data), a partir de <paramref name="from"/> (padrão: HEAD).</summary>
    public IReadOnlyList<CommitInfo> Log(int maxCount = 100, ObjectId? from = null)
    {
        var start = from ?? Head;
        var result = new List<CommitInfo>();
        if (start is null) return result;
        var queue = new PriorityQueue<CommitInfo, long>();
        var seen = new HashSet<ObjectId>();
        void Enqueue(ObjectId id)
        {
            if (!seen.Add(id)) return;
            var commit = GetCommit(id);
            queue.Enqueue(commit, -commit.Committer.When.ToUnixTimeSeconds());
        }
        Enqueue(start.Value);
        while (queue.Count > 0 && result.Count < maxCount)
        {
            var commit = queue.Dequeue();
            result.Add(commit);
            foreach (var parent in commit.Parents) Enqueue(parent);
        }
        return result;
    }

    public bool IsAncestor(ObjectId ancestor, ObjectId descendant)
    {
        if (ancestor == descendant) return true;
        var seen = new HashSet<ObjectId>();
        var stack = new Stack<ObjectId>();
        stack.Push(descendant);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!seen.Add(id)) continue;
            foreach (var parent in GetCommit(id).Parents)
            {
                if (parent == ancestor) return true;
                stack.Push(parent);
            }
        }
        return false;
    }

    public ObjectId? MergeBase(ObjectId a, ObjectId b)
    {
        var ancestorsOfA = new HashSet<ObjectId>();
        var stack = new Stack<ObjectId>();
        stack.Push(a);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!ancestorsOfA.Add(id)) continue;
            foreach (var parent in GetCommit(id).Parents) stack.Push(parent);
        }
        var queue = new PriorityQueue<CommitInfo, long>();
        var seen = new HashSet<ObjectId>();
        void Enqueue(ObjectId id)
        {
            if (!seen.Add(id)) return;
            var commit = GetCommit(id);
            queue.Enqueue(commit, -commit.Committer.When.ToUnixTimeSeconds());
        }
        Enqueue(b);
        while (queue.Count > 0)
        {
            var commit = queue.Dequeue();
            if (ancestorsOfA.Contains(commit.Id)) return commit.Id;
            foreach (var parent in commit.Parents) Enqueue(parent);
        }
        return null;
    }
}
