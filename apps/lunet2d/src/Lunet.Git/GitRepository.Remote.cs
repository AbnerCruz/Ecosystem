using System.Text;

namespace Lunet.Git;

public sealed record RemoteInfo(string Name, string Url);

public sealed record FetchResult(int ObjectsReceived, IReadOnlyList<string> UpdatedRefs, string? DefaultBranch);

public sealed record PushResult(bool UpToDate, string Branch, ObjectId? NewId);

public sealed partial class GitRepository
{
    private const string Agent = "agent=lunet2d";

    // ---------- Remotos ----------

    public IReadOnlyList<RemoteInfo> Remotes()
    {
        var config = Config;
        return config.Subsections("remote").Select(name => new RemoteInfo(name, config.Get("remote", name, "url") ?? "")).OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
    }

    public void SetRemote(string name, string url)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !char.IsLetterOrDigit(c) && c is not ('-' or '_' or '.'))) throw new GitException("Nome de remoto inválido.");
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            throw new GitException("Use um endereço https, por exemplo https://github.com/usuario/projeto.git");
        var config = Config;
        config.Set("remote", name, "url", url);
        config.Set("remote", name, "fetch", $"+refs/heads/*:refs/remotes/{name}/*");
        config.Save();
    }

    public void RemoveRemote(string name)
    {
        var config = Config;
        config.RemoveSection("remote", name);
        config.Save();
    }

    // ---------- Buscar (fetch) ----------

    public async Task<FetchResult> FetchAsync(string remote, IGitTransport transport, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        progress?.Report("Consultando o servidor…");
        var advertisement = Advertisement.Parse(await transport.AdvertiseAsync("git-upload-pack", cancellation).ConfigureAwait(false));
        var branches = advertisement.Refs.Where(kv => kv.Key.StartsWith("refs/heads/", StringComparison.Ordinal)).ToList();
        var wants = branches.Select(kv => kv.Value).Distinct().Where(id => !Objects.Contains(id)).ToList();
        var received = 0;

        if (wants.Count > 0)
        {
            var sideBand = advertisement.Capabilities.Contains("side-band-64k");
            var capabilities = new List<string>();
            if (sideBand) capabilities.Add("side-band-64k");
            if (advertisement.Capabilities.Contains("ofs-delta")) capabilities.Add("ofs-delta");
            capabilities.Add(Agent);

            using var request = new MemoryStream();
            for (var i = 0; i < wants.Count; i++)
                request.Write(PktLine.Encode($"want {wants[i].Hex}{(i == 0 ? " " + string.Join(' ', capabilities) : "")}\n"));
            request.Write(PktLine.Flush);
            foreach (var have in LocalTips().Take(256)) request.Write(PktLine.Encode($"have {have.Hex}\n"));
            request.Write(PktLine.Encode("done\n"));

            progress?.Report($"Baixando {wants.Count} ramo(s)…");
            var response = await transport.RpcAsync("git-upload-pack", request.ToArray(), cancellation).ConfigureAwait(false);
            var pack = ExtractPack(response, sideBand, progress);
            progress?.Report("Gravando objetos…");
            received = PackFormat.Unpack(pack, Objects);
            Objects.ReloadPacks();
        }

        var updated = new List<string>();
        foreach (var (name, id) in branches)
        {
            var tracking = $"refs/remotes/{remote}/{name["refs/heads/".Length..]}";
            if (ResolveRef(tracking) == id) continue;
            UpdateRef(tracking, id);
            updated.Add(tracking["refs/remotes/".Length..]);
        }
        var defaultBranch = advertisement.HeadTarget is { } target && target.StartsWith("refs/heads/", StringComparison.Ordinal) ? target["refs/heads/".Length..] : null;
        return new FetchResult(received, updated, defaultBranch);
    }

    private IEnumerable<ObjectId> LocalTips()
    {
        var tips = new List<ObjectId>();
        if (Head is { } head) tips.Add(head);
        tips.AddRange(Branches(includeRemote: true).Select(b => b.Id));
        var seen = new HashSet<ObjectId>();
        foreach (var tip in tips.Distinct())
            foreach (var commit in Log(64, tip))
                if (seen.Add(commit.Id)) yield return commit.Id;
    }

    private static byte[] ExtractPack(byte[] response, bool sideBand, IProgress<string>? progress)
    {
        var position = 0;
        using var pack = new MemoryStream();
        while (PktLine.TryRead(response, ref position, out var payload))
        {
            if (payload is null) continue;
            if (sideBand)
            {
                switch (payload[0])
                {
                    case 1: pack.Write(payload, 1, payload.Length - 1); continue;
                    case 2:
                        var message = Encoding.UTF8.GetString(payload, 1, payload.Length - 1).Trim();
                        if (message.Length > 0) progress?.Report(message.Split('\r', '\n').Last(l => l.Length > 0));
                        continue;
                    case 3: throw new GitException("O servidor recusou: " + Encoding.UTF8.GetString(payload, 1, payload.Length - 1).Trim());
                }
            }
            var text = Encoding.ASCII.GetString(payload, 0, Math.Min(payload.Length, 4));
            if (text.StartsWith("NAK", StringComparison.Ordinal) || text.StartsWith("ACK", StringComparison.Ordinal)) continue;
            if (text.StartsWith("ERR", StringComparison.Ordinal)) throw new GitException("O servidor recusou: " + Encoding.UTF8.GetString(payload).Trim());
            if (!sideBand)
            {
                // Sem side-band o pacote vem cru logo depois dos ACK/NAK.
                pack.Write(payload);
                pack.Write(response, position, response.Length - position);
                break;
            }
        }
        var bytes = pack.ToArray();
        if (bytes.Length == 0) throw new GitException("O servidor não enviou objetos.");
        return bytes;
    }

    // ---------- Enviar (push) ----------

    /// <summary>Envia o ramo local para o remoto. Recusa quando o servidor tem commits que faltam aqui (faça pull) — a não ser com <paramref name="force"/>.</summary>
    public async Task<PushResult> PushAsync(string remote, string branch, IGitTransport transport, bool force = false, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        var local = ResolveRef("refs/heads/" + branch) ?? throw new GitException($"O ramo \"{branch}\" não existe.");
        progress?.Report("Consultando o servidor…");
        var advertisement = Advertisement.Parse(await transport.AdvertiseAsync("git-receive-pack", cancellation).ConfigureAwait(false));
        var reference = "refs/heads/" + branch;
        var old = advertisement.Refs.TryGetValue(reference, out var remoteId) ? remoteId : ObjectId.Zero;
        if (old == local)
        {
            UpdateRef($"refs/remotes/{remote}/{branch}", local);
            return new PushResult(true, branch, local);
        }
        if (!old.IsZero)
        {
            if (!Objects.Contains(old) && !force) throw new GitException("O servidor tem commits que você ainda não baixou. Faça pull antes de enviar.");
            if (Objects.Contains(old) && !IsAncestor(old, local) && !force)
                throw new GitException("Envio recusado: o ramo remoto tem commits que faltam aqui. Faça pull, resolva e envie de novo.");
        }

        progress?.Report("Preparando objetos…");
        var known = advertisement.Refs.Values.Where(id => Objects.Contains(id)).Distinct().ToList();
        var already = ReachableObjects(known);
        var toSend = ReachableObjects([local]).Where(id => !already.Contains(id)).ToList();
        var objects = toSend.Select(id =>
        {
            var obj = Objects.Read(id);
            return (obj.Type, obj.Data);
        }).ToList();
        var pack = PackFormat.Build(objects);

        var capabilities = new List<string>();
        if (advertisement.Capabilities.Contains("report-status")) capabilities.Add("report-status");
        var sideBand = advertisement.Capabilities.Contains("side-band-64k");
        if (sideBand) capabilities.Add("side-band-64k");
        capabilities.Add(Agent);

        using var request = new MemoryStream();
        request.Write(PktLine.Encode($"{old.Hex} {local.Hex} {reference}\0 {string.Join(' ', capabilities)}"));
        request.Write(PktLine.Flush);
        request.Write(pack);

        progress?.Report($"Enviando {objects.Count} objeto(s)…");
        var response = await transport.RpcAsync("git-receive-pack", request.ToArray(), cancellation).ConfigureAwait(false);
        CheckPushReport(response, sideBand, reference, capabilities.Contains("report-status"));
        UpdateRef($"refs/remotes/{remote}/{branch}", local);
        return new PushResult(false, branch, local);
    }

    private static void CheckPushReport(byte[] response, bool sideBand, string reference, bool expectReport)
    {
        var report = response;
        if (sideBand)
        {
            using var band1 = new MemoryStream();
            var position = 0;
            while (PktLine.TryRead(response, ref position, out var payload))
            {
                if (payload is null) continue;
                if (payload[0] == 1) band1.Write(payload, 1, payload.Length - 1);
                else if (payload[0] == 3) throw new GitException("O servidor recusou o envio: " + Encoding.UTF8.GetString(payload, 1, payload.Length - 1).Trim());
            }
            report = band1.ToArray();
        }
        if (!expectReport) return;
        var offset = 0;
        var unpackOk = false;
        while (PktLine.TryRead(report, ref offset, out var line))
        {
            if (line is null) continue;
            var text = Encoding.UTF8.GetString(line).TrimEnd('\n');
            if (text == "unpack ok") unpackOk = true;
            else if (text.StartsWith("unpack ", StringComparison.Ordinal)) throw new GitException("O servidor não conseguiu abrir o pacote: " + text[7..]);
            else if (text.StartsWith("ng ", StringComparison.Ordinal)) throw new GitException("O servidor recusou " + text[3..]);
            else if (text.StartsWith("ERR ", StringComparison.Ordinal)) throw new GitException("O servidor recusou: " + text[4..]);
            else if (text.StartsWith("ok " + reference, StringComparison.Ordinal)) return;
        }
        if (!unpackOk) throw new GitException("O servidor não confirmou o envio.");
    }

    /// <summary>Todos os objetos (commits, árvores e blobs) alcançáveis a partir dos commits dados.</summary>
    public HashSet<ObjectId> ReachableObjects(IEnumerable<ObjectId> tips)
    {
        var seen = new HashSet<ObjectId>();
        var pending = new Stack<ObjectId>(tips);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!seen.Add(id)) continue;
            var obj = Objects.Read(id);
            switch (obj.Type)
            {
                case ObjectType.Commit:
                    var commit = CommitInfo.Parse(id, obj.Data);
                    pending.Push(commit.Tree);
                    foreach (var parent in commit.Parents) pending.Push(parent);
                    break;
                case ObjectType.Tree:
                    foreach (var entry in TreeFormat.Parse(obj.Data))
                        if (entry.Mode != "160000") pending.Push(entry.Id);
                    break;
            }
        }
        return seen;
    }

    // ---------- Pull e clone ----------

    /// <summary>Busca e mescla o ramo remoto de mesmo nome no ramo atual.</summary>
    public async Task<MergeOutcome> PullAsync(string remote, IGitTransport transport, GitSignature who, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        var branch = CurrentBranch ?? throw new GitException("Volte para um ramo antes de fazer pull (o HEAD está destacado).");
        await FetchAsync(remote, transport, progress, cancellation).ConfigureAwait(false);
        var tip = ResolveRef($"refs/remotes/{remote}/{branch}") ?? throw new GitException($"O ramo \"{branch}\" ainda não existe no servidor. Faça push primeiro.");
        progress?.Report("Mesclando…");
        return Merge(tip, who, $"Mesclar {remote}/{branch} em {branch}");
    }

    public static async Task<GitRepository> CloneAsync(string url, string directory, IGitTransport transport, IProgress<string>? progress = null, CancellationToken cancellation = default)
    {
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new GitException("A pasta de destino não está vazia.");
        Directory.CreateDirectory(directory);
        var repository = Init(directory);
        try
        {
            repository.SetRemote("origin", url);
            var fetched = await repository.FetchAsync("origin", transport, progress, cancellation).ConfigureAwait(false);
            var branches = repository.Branches(includeRemote: true).Where(b => b.IsRemote && b.Name.StartsWith("origin/", StringComparison.Ordinal)).ToList();
            if (branches.Count == 0) return repository; // servidor vazio
            var chosen = branches.FirstOrDefault(b => b.Name == "origin/" + fetched.DefaultBranch)
                         ?? branches.FirstOrDefault(b => b.Name == "origin/main") ?? branches.FirstOrDefault(b => b.Name == "origin/master") ?? branches[0];
            var name = chosen.Name["origin/".Length..];
            repository.UpdateRef("refs/heads/" + name, chosen.Id);
            repository.SetHeadToBranch(name);
            repository.ResetHard(chosen.Id);
            return repository;
        }
        catch
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>Faz o diretório de trabalho e o índice ficarem exatamente como o commit (apaga mudanças locais em arquivos rastreados).</summary>
    public void ResetHard(ObjectId commit)
    {
        var desired = Flatten(GetCommit(commit).Tree);
        var current = LoadIndex();
        foreach (var gone in current.Entries.Where(e => !desired.ContainsKey(e.Path)).Select(e => e.Path).ToList())
        {
            var absolute = WorkPath(gone);
            if (File.Exists(absolute)) File.Delete(absolute);
            RemoveEmptyParents(absolute);
        }
        var index = new GitIndex();
        foreach (var (path, entry) in desired)
        {
            WriteBlob(WorkPath(path), entry.Id);
            index.Set(EntryFor(path, entry.Mode, entry.Id));
        }
        SaveIndex(index);
    }
}
