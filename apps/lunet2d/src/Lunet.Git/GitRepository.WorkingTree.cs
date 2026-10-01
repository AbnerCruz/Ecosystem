using System.Text;

namespace Lunet.Git;

public sealed partial class GitRepository
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    // ---------- Arquivos do diretório de trabalho ----------

    /// <summary>Caminhos relativos (com '/') dos arquivos do projeto que não são ignorados. O Git em si (.git) nunca entra.</summary>
    public IReadOnlyList<string> WorkFiles()
    {
        var result = new List<string>();
        Walk(WorkDirectory, "", [], result);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private void Walk(string absolute, string relative, List<(string Prefix, GitIgnore Rules)> ignores, List<string> result)
    {
        var local = ignores;
        var ignoreFile = Path.Combine(absolute, ".gitignore");
        if (File.Exists(ignoreFile))
        {
            local = [.. ignores, (relative, GitIgnore.Parse(File.ReadAllText(ignoreFile)))];
        }
        foreach (var file in Directory.EnumerateFiles(absolute).OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".lunet-tmp", StringComparison.Ordinal) || name.EndsWith(".lock", StringComparison.Ordinal) && absolute == GitDirectory) continue;
            var path = relative + name;
            if (!IsIgnored(local, path, isDirectory: false)) result.Add(path);
        }
        foreach (var directory in Directory.EnumerateDirectories(absolute).OrderBy(d => d, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(directory);
            if (name == ".git") continue;
            var path = relative + name;
            if (IsIgnored(local, path, isDirectory: true)) continue;
            Walk(directory, path + "/", local, result);
        }
    }

    private static bool IsIgnored(List<(string Prefix, GitIgnore Rules)> ignores, string path, bool isDirectory)
    {
        bool? decision = null;
        foreach (var (prefix, rules) in ignores)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (rules.IsIgnored(path[prefix.Length..], isDirectory) is { } verdict) decision = verdict;
        }
        return decision == true;
    }

    private static (long Seconds, int Nanos) Stamp(string file)
    {
        var time = File.GetLastWriteTimeUtc(file);
        var offset = new DateTimeOffset(time);
        return (offset.ToUnixTimeSeconds(), (int)(time.Ticks % TimeSpan.TicksPerSecond * 100));
    }

    private IndexEntry EntryFor(string path, string mode, ObjectId id)
    {
        var file = WorkPath(path);
        var info = new FileInfo(file);
        var (seconds, nanos) = Stamp(file);
        return new IndexEntry(path, mode, id, info.Length, seconds, nanos);
    }

    private bool WorkFileChanged(IndexEntry entry)
    {
        var file = WorkPath(entry.Path);
        if (!File.Exists(file)) return true;
        var info = new FileInfo(file);
        var (seconds, _) = Stamp(file);
        // Atalho pelo "stat"; arquivos mexidos há poucos segundos podem ter mudado sem alterar tamanho nem hora (git "racy"): confere o conteúdo.
        var recent = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - entry.ModifiedSeconds < 3;
        if (!recent && info.Length == entry.Size && seconds == entry.ModifiedSeconds) return false;
        return ObjectId.Compute(ObjectType.Blob, File.ReadAllBytes(file)) != entry.Id;
    }

    // ---------- Status ----------

    public IReadOnlyList<StatusEntry> GetStatus()
    {
        var head = Flatten(HeadTree());
        var index = LoadIndex();
        var work = new HashSet<string>(WorkFiles(), StringComparer.Ordinal);
        var paths = new SortedSet<string>(head.Keys.Concat(index.Entries.Select(e => e.Path)).Concat(work), StringComparer.Ordinal);
        var result = new List<StatusEntry>();
        foreach (var path in paths)
        {
            var entry = index.Get(path);
            FileChange staged = FileChange.None, unstaged = FileChange.None;
            if (entry is not null)
            {
                if (!head.TryGetValue(path, out var inHead)) staged = FileChange.Added;
                else if (inHead.Id != entry.Id || inHead.Mode != entry.Mode) staged = FileChange.Modified;
                var exists = File.Exists(WorkPath(path));
                if (!exists) unstaged = FileChange.Deleted;
                else if (WorkFileChanged(entry)) unstaged = FileChange.Modified;
            }
            else
            {
                if (head.ContainsKey(path)) staged = FileChange.Deleted;
                if (work.Contains(path)) unstaged = FileChange.Untracked;
            }
            if (staged != FileChange.None || unstaged != FileChange.None) result.Add(new StatusEntry(path, staged, unstaged));
        }
        return result;
    }

    // ---------- Preparar (stage) ----------

    /// <summary>Prepara um arquivo (novo, alterado ou apagado) ou todos os arquivos de uma pasta.</summary>
    public void Stage(string path)
    {
        var index = LoadIndex();
        StageInto(index, path);
        SaveIndex(index);
    }

    public void StageAll()
    {
        var index = LoadIndex();
        foreach (var status in GetStatus().Where(s => s.Unstaged != FileChange.None)) StageInto(index, status.Path);
        SaveIndex(index);
    }

    private void StageInto(GitIndex index, string path)
    {
        path = path.TrimEnd('/');
        var absolute = WorkPath(path);
        if (Directory.Exists(absolute))
        {
            var prefix = path + "/";
            foreach (var file in WorkFiles().Where(f => f.StartsWith(prefix, StringComparison.Ordinal))) StageInto(index, file);
            foreach (var gone in index.Entries.Where(e => e.Path.StartsWith(prefix, StringComparison.Ordinal) && !File.Exists(WorkPath(e.Path))).Select(e => e.Path).ToList())
                index.Remove(gone);
            return;
        }
        if (!File.Exists(absolute))
        {
            index.Remove(path);
            return;
        }
        var id = Objects.Write(ObjectType.Blob, File.ReadAllBytes(absolute));
        var mode = index.Get(path)?.Mode ?? "100644";
        index.Set(EntryFor(path, mode, id));
    }

    /// <summary>Tira um arquivo da preparação (volta ao que está no último commit).</summary>
    public void Unstage(string path)
    {
        var index = LoadIndex();
        var head = Flatten(HeadTree());
        foreach (var candidate in PathsUnder(path, head.Keys.Concat(index.Entries.Select(e => e.Path))))
        {
            if (head.TryGetValue(candidate, out var inHead))
            {
                var file = WorkPath(candidate);
                var stamp = File.Exists(file) ? Stamp(file) : (0L, 0);
                index.Set(new IndexEntry(candidate, inHead.Mode, inHead.Id, File.Exists(file) ? new FileInfo(file).Length : 0, stamp.Item1, stamp.Item2));
            }
            else index.Remove(candidate);
        }
        SaveIndex(index);
    }

    private static IEnumerable<string> PathsUnder(string path, IEnumerable<string> all)
    {
        path = path.TrimEnd('/');
        return all.Where(p => p == path || p.StartsWith(path + "/", StringComparison.Ordinal)).Distinct().ToList();
    }

    // ---------- Commit ----------

    public ObjectId Commit(string message, GitSignature author, GitSignature? committer = null, bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new GitException("Escreva uma mensagem para o commit.");
        var index = LoadIndex();
        var tree = WriteTree(index);
        var head = Head;
        if (!allowEmpty && HeadTree() == tree) throw new GitException("Nada para commitar: prepare (stage) alguma mudança antes.");
        return CommitTree(tree, head is null ? [] : [head.Value], message, author, committer ?? author);
    }

    private ObjectId CommitTree(ObjectId tree, IEnumerable<ObjectId> parents, string message, GitSignature author, GitSignature committer)
    {
        var id = Objects.Write(ObjectType.Commit, CommitInfo.Serialize(tree, parents, author, committer, message));
        MoveHead(id);
        return id;
    }

    // ---------- Diff ----------

    private string ReadText(ObjectId? id, out bool binary)
    {
        binary = false;
        if (id is null) return "";
        var data = Objects.Read(id.Value).Data;
        if (Array.IndexOf(data, (byte)0) >= 0) { binary = true; return ""; }
        return Utf8.GetString(data);
    }

    /// <summary>Diff das mudanças ainda não preparadas de um arquivo (índice × diretório de trabalho).</summary>
    public string DiffWorking(string path)
    {
        var entry = LoadIndex().Get(path);
        var file = WorkPath(path);
        var oldText = ReadText(entry?.Id, out var binaryOld);
        var exists = File.Exists(file);
        var bytes = exists ? File.ReadAllBytes(file) : [];
        if (binaryOld || Array.IndexOf(bytes, (byte)0) >= 0) return $"Arquivos binários {path} diferem\n";
        return LineDiff.Unified(entry is null ? "/dev/null" : "a/" + path, exists ? "b/" + path : "/dev/null", oldText, Utf8.GetString(bytes));
    }

    /// <summary>Diff das mudanças preparadas de um arquivo (último commit × índice).</summary>
    public string DiffStaged(string path)
    {
        var head = Flatten(HeadTree());
        var entry = LoadIndex().Get(path);
        var oldText = ReadText(head.TryGetValue(path, out var inHead) ? inHead.Id : null, out var binaryOld);
        var newText = ReadText(entry?.Id, out var binaryNew);
        if (binaryOld || binaryNew) return $"Arquivos binários {path} diferem\n";
        return LineDiff.Unified(inHead.Id == default ? "/dev/null" : "a/" + path, entry is null ? "/dev/null" : "b/" + path, oldText, newText);
    }

    /// <summary>Arquivos alterados por um commit em relação ao primeiro pai.</summary>
    public IReadOnlyList<FileDiffInfo> CommitChanges(ObjectId id)
    {
        var commit = GetCommit(id);
        var before = Flatten(commit.Parents.Count > 0 ? GetCommit(commit.Parents[0]).Tree : null);
        var after = Flatten(commit.Tree);
        var result = new List<FileDiffInfo>();
        foreach (var path in before.Keys.Concat(after.Keys).Distinct().OrderBy(p => p, StringComparer.Ordinal))
        {
            var hasBefore = before.TryGetValue(path, out var oldEntry);
            var hasAfter = after.TryGetValue(path, out var newEntry);
            if (hasBefore && hasAfter && oldEntry.Id == newEntry.Id && oldEntry.Mode == newEntry.Mode) continue;
            result.Add(new FileDiffInfo(path, !hasBefore ? FileChange.Added : !hasAfter ? FileChange.Deleted : FileChange.Modified));
        }
        return result;
    }

    /// <summary>Diff de um arquivo dentro de um commit (em relação ao primeiro pai).</summary>
    public string DiffCommitFile(ObjectId id, string path)
    {
        var commit = GetCommit(id);
        var before = Flatten(commit.Parents.Count > 0 ? GetCommit(commit.Parents[0]).Tree : null);
        var after = Flatten(commit.Tree);
        var hasBefore = before.TryGetValue(path, out var oldEntry);
        var hasAfter = after.TryGetValue(path, out var newEntry);
        var oldText = ReadText(hasBefore ? oldEntry.Id : null, out var binaryOld);
        var newText = ReadText(hasAfter ? newEntry.Id : null, out var binaryNew);
        if (binaryOld || binaryNew) return $"Arquivos binários {path} diferem\n";
        return LineDiff.Unified(hasBefore ? "a/" + path : "/dev/null", hasAfter ? "b/" + path : "/dev/null", oldText, newText);
    }

    // ---------- Descartar / restaurar ----------

    /// <summary>Desfaz as mudanças não preparadas de um arquivo (volta ao índice); arquivo novo sem rastreio é apagado.</summary>
    public void DiscardChanges(string path)
    {
        var index = LoadIndex();
        foreach (var candidate in PathsUnder(path, index.Entries.Select(e => e.Path).Concat(WorkFiles())))
        {
            var entry = index.Get(candidate);
            var absolute = WorkPath(candidate);
            if (entry is null)
            {
                if (File.Exists(absolute)) File.Delete(absolute);
                RemoveEmptyParents(absolute);
                continue;
            }
            WriteBlob(absolute, entry.Id);
            index.Set(EntryFor(candidate, entry.Mode, entry.Id));
        }
        SaveIndex(index);
    }

    /// <summary>Restaura um arquivo com o conteúdo de um commit (fica como mudança não preparada... e preparada: índice também).</summary>
    public void RestoreFromCommit(string path, ObjectId commitId)
    {
        var tree = Flatten(GetCommit(commitId).Tree);
        if (!tree.TryGetValue(path, out var entry)) throw new GitException($"O arquivo {path} não existe no commit {commitId.Short}.");
        WriteBlob(WorkPath(path), entry.Id);
    }

    private void WriteBlob(string absolute, ObjectId id)
    {
        var blob = Objects.Read(id);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, blob.Data);
    }

    private void RemoveEmptyParents(string absolute)
    {
        var directory = Path.GetDirectoryName(absolute);
        while (directory is not null && directory.Length > WorkDirectory.Length && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    // ---------- Trocar de ramo / aplicar árvore ----------

    /// <summary>Troca para um ramo (ou commit, com HEAD destacado). Recusa se mudanças locais seriam perdidas.</summary>
    public void Checkout(string branchOrCommit)
    {
        ObjectId target;
        string? branch = null;
        if (ResolveRef("refs/heads/" + branchOrCommit) is { } tip) { target = tip; branch = branchOrCommit; }
        else if (ObjectId.TryParse(branchOrCommit, out var exact) && Objects.Contains(exact)) target = exact;
        else if (ResolveRef("refs/remotes/" + branchOrCommit) is { } remoteTip && branchOrCommit.Contains('/'))
        {
            var local = branchOrCommit[(branchOrCommit.IndexOf('/') + 1)..];
            if (ResolveRef("refs/heads/" + local) is not null) throw new GitException($"O ramo local \"{local}\" já existe; troque para ele.");
            target = remoteTip;
            branch = local;
            UpdateRef("refs/heads/" + local, target);
        }
        else throw new GitException($"Ramo ou commit \"{branchOrCommit}\" não encontrado.");

        ApplyCommit(target);
        if (branch is not null) SetHeadToBranch(branch);
        else SetHeadDetached(target);
    }

    public void CheckoutNewBranch(string name)
    {
        CreateBranch(name);
        SetHeadToBranch(name); // mesmo commit: a árvore não muda
    }

    /// <summary>Faz o índice e o diretório de trabalho refletirem o commit, preservando mudanças locais em arquivos que ele não altera.</summary>
    private void ApplyCommit(ObjectId target)
    {
        var current = Flatten(HeadTree());
        var desired = Flatten(GetCommit(target).Tree);
        ApplyTree(current, desired);
    }

    private void ApplyTree(IReadOnlyDictionary<string, (string Mode, ObjectId Id)> from, IReadOnlyDictionary<string, (string Mode, ObjectId Id)> to)
    {
        var index = LoadIndex();
        var changed = from.Keys.Concat(to.Keys).Distinct()
            .Where(p => from.ContainsKey(p) != to.ContainsKey(p) || from[p].Id != to[p].Id || from[p].Mode != to[p].Mode)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var conflicts = new List<string>();
        foreach (var path in changed)
        {
            var entry = index.Get(path);
            var absolute = WorkPath(path);
            var inFrom = from.TryGetValue(path, out var fromEntry);
            if (inFrom)
            {
                // Preparado diferente do commit atual, ou arquivo modificado/apagado no disco.
                if (entry is null || entry.Id != fromEntry.Id || WorkFileChanged(entry)) conflicts.Add(path);
            }
            else if (File.Exists(absolute) || entry is not null) conflicts.Add(path); // arquivo novo do usuário seria sobrescrito
        }
        if (conflicts.Count > 0)
            throw new GitException("Suas alterações locais seriam perdidas nestes arquivos: " + string.Join(", ", conflicts.Take(5)) + (conflicts.Count > 5 ? $" e mais {conflicts.Count - 5}" : "") + ". Faça commit ou descarte-as antes.");

        foreach (var path in changed)
        {
            var absolute = WorkPath(path);
            if (to.TryGetValue(path, out var entry))
            {
                WriteBlob(absolute, entry.Id);
                index.Set(EntryFor(path, entry.Mode, entry.Id));
            }
            else
            {
                if (File.Exists(absolute)) File.Delete(absolute);
                RemoveEmptyParents(absolute);
                index.Remove(path);
            }
        }
        SaveIndex(index);
    }

    // ---------- Reverter e mesclar ----------

    /// <summary>Cria um commit que desfaz as mudanças de <paramref name="id"/>. Recusa se o arquivo mudou depois disso de forma incompatível.</summary>
    public ObjectId Revert(ObjectId id, GitSignature author)
    {
        var commit = GetCommit(id);
        if (commit.Parents.Count != 1) throw new GitException("Só é possível reverter commits com um pai.");
        EnsureClean();
        var parent = Flatten(GetCommit(commit.Parents[0]).Tree);
        var changed = Flatten(commit.Tree);
        var current = Flatten(HeadTree());
        var desired = new Dictionary<string, (string Mode, ObjectId Id)>(current, StringComparer.Ordinal);
        var conflicts = new List<string>();
        foreach (var path in parent.Keys.Concat(changed.Keys).Distinct())
        {
            var hasParent = parent.TryGetValue(path, out var parentEntry);
            var hasChanged = changed.TryGetValue(path, out var changedEntry);
            if (hasParent && hasChanged && parentEntry.Id == changedEntry.Id && parentEntry.Mode == changedEntry.Mode) continue;
            var hasCurrent = current.TryGetValue(path, out var currentEntry);
            var matchesCommit = hasCurrent == hasChanged && (!hasCurrent || currentEntry.Id == changedEntry.Id);
            var matchesParent = hasCurrent == hasParent && (!hasCurrent || currentEntry.Id == parentEntry.Id);
            if (matchesParent) continue;
            if (!matchesCommit) { conflicts.Add(path); continue; }
            if (hasParent) desired[path] = parentEntry; else desired.Remove(path);
        }
        if (conflicts.Count > 0)
            throw new GitException("Não dá para reverter automaticamente: estes arquivos mudaram depois: " + string.Join(", ", conflicts.Take(5)) + ".");

        ApplyTree(current, desired);
        var index = LoadIndex();
        var tree = WriteTree(index);
        var message = $"Reverter \"{commit.Summary}\"\n\nIsto reverte o commit {id.Hex}.";
        return CommitTree(tree, [Head!.Value], message, author, author);
    }

    /// <summary>Verifica que não há mudanças pendentes em arquivos rastreados.</summary>
    public void EnsureClean()
    {
        var dirty = GetStatus().Where(s => s.Staged != FileChange.None || s.Unstaged is FileChange.Modified or FileChange.Deleted).Select(s => s.Path).ToList();
        if (dirty.Count > 0) throw new GitException("Há mudanças não commitadas: " + string.Join(", ", dirty.Take(5)) + (dirty.Count > 5 ? "…" : "") + ". Faça commit ou descarte antes.");
    }

    /// <summary>Mescla o commit <paramref name="theirs"/> no ramo atual: avanço rápido quando possível, senão mescla arquivo a arquivo (sem conflitos).</summary>
    public MergeOutcome Merge(ObjectId theirs, GitSignature author, string? message = null)
    {
        var ours = Head;
        if (ours is null)
        {
            ApplyCommit(theirs);
            MoveHead(theirs);
            return MergeOutcome.FastForward;
        }
        if (IsAncestor(theirs, ours.Value)) return MergeOutcome.UpToDate;
        EnsureClean();
        if (IsAncestor(ours.Value, theirs))
        {
            ApplyCommit(theirs);
            MoveHead(theirs);
            return MergeOutcome.FastForward;
        }

        var baseId = MergeBase(ours.Value, theirs);
        var baseTree = Flatten(baseId is { } b ? GetCommit(b).Tree : null);
        var ourTree = Flatten(GetCommit(ours.Value).Tree);
        var theirTree = Flatten(GetCommit(theirs).Tree);
        var merged = new Dictionary<string, (string Mode, ObjectId Id)>(ourTree, StringComparer.Ordinal);
        var conflicts = new List<string>();
        foreach (var path in baseTree.Keys.Concat(ourTree.Keys).Concat(theirTree.Keys).Distinct())
        {
            var hasBase = baseTree.TryGetValue(path, out var baseEntry);
            var hasOurs = ourTree.TryGetValue(path, out var ourEntry);
            var hasTheirs = theirTree.TryGetValue(path, out var theirEntry);
            var oursSame = hasBase == hasOurs && (!hasBase || baseEntry.Id == ourEntry.Id);
            var theirsSame = hasBase == hasTheirs && (!hasBase || baseEntry.Id == theirEntry.Id);
            if (theirsSame) continue; // só o nosso lado mudou (ou nada)
            if (oursSame)
            {
                if (hasTheirs) merged[path] = theirEntry; else merged.Remove(path);
                continue;
            }
            if (hasOurs == hasTheirs && (!hasOurs || ourEntry.Id == theirEntry.Id)) continue; // mudaram igual
            conflicts.Add(path);
        }
        if (conflicts.Count > 0)
            throw new GitException("Conflito ao mesclar: os dois lados alteraram " + string.Join(", ", conflicts.Take(5)) + ". Resolva os conflitos manualmente em outro cliente Git.");

        ApplyTree(ourTree, merged);
        var index = LoadIndex();
        var tree = WriteTree(index);
        var text = message ?? $"Mesclar {theirs.Short} em {CurrentBranch ?? "HEAD"}";
        CommitTree(tree, [ours.Value, theirs], text, author, author);
        return MergeOutcome.Merged;
    }
}
