using System.Diagnostics;
using Lunet.Git;

namespace Lunet.Tests;

/// <summary>Transporte que fala o mesmo protocolo do HTTP, mas por processos locais do git: valida nosso cliente contra o git de verdade.</summary>
internal sealed class ProcessGitTransport(string remoteDirectory) : IGitTransport
{
    public Task<byte[]> AdvertiseAsync(string service, CancellationToken cancellation) =>
        Task.FromResult(Run(service.Replace("git-", ""), ["--stateless-rpc", "--advertise-refs", remoteDirectory], []));

    public Task<byte[]> RpcAsync(string service, byte[] request, CancellationToken cancellation) =>
        Task.FromResult(Run(service.Replace("git-", ""), ["--stateless-rpc", remoteDirectory], request));

    private static byte[] Run(string command, string[] arguments, byte[] input)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(command);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";
        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        var outputTask = Task.Run(() =>
        {
            using var buffer = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            return buffer.ToArray();
        });
        process.StandardInput.BaseStream.Write(input);
        process.StandardInput.Close();
        var output = outputTask.Result;
        process.WaitForExit();
        if (process.ExitCode != 0 && output.Length == 0) throw new GitException("git falhou: " + errors.Result);
        return output;
    }
}

public sealed class GitRemoteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-gitremote-" + Guid.NewGuid().ToString("N"));
    private static readonly GitSignature Me = new("Ana", "ana@example.com", new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));

    public GitRemoteTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private string Dir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Git(string dir, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";
        start.Environment["GIT_AUTHOR_NAME"] = "Bia";
        start.Environment["GIT_AUTHOR_EMAIL"] = "bia@example.com";
        start.Environment["GIT_COMMITTER_NAME"] = "Bia";
        start.Environment["GIT_COMMITTER_EMAIL"] = "bia@example.com";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} falhou: {error}");
        return output;
    }

    private static void Write(string dir, string relative, string text)
    {
        var path = Path.Combine(dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private string BareRemote()
    {
        var remote = Dir("remote.git");
        Git(remote, "init", "-q", "--bare", "-b", "main");
        return remote;
    }

    [Fact]
    public async Task Push_SendsCommitsThatRealGitAccepts_AndIsIdempotent()
    {
        var remote = BareRemote();
        var transport = new ProcessGitTransport(remote);
        var dir = Dir("local");
        var repo = GitRepository.Init(dir);
        Write(dir, "Game.cs", "class A { }\n");
        Write(dir, "Content/data.json", "{ }\n");
        repo.StageAll();
        var first = repo.Commit("primeiro", Me);
        repo.SetRemote("origin", "https://example.com/x.git");

        var result = await repo.PushAsync("origin", "main", transport, cancellation: TestContext.Current.CancellationToken);
        Assert.False(result.UpToDate);
        Assert.Equal(first.Hex, Git(remote, "rev-parse", "main").Trim());
        Git(remote, "fsck", "--strict");
        Assert.Equal(first, repo.ResolveRef("refs/remotes/origin/main"));

        Assert.True((await repo.PushAsync("origin", "main", transport, cancellation: TestContext.Current.CancellationToken)).UpToDate);

        Write(dir, "Game.cs", "class A { int x; }\n");
        repo.StageAll();
        var second = repo.Commit("segundo", Me);
        await repo.PushAsync("origin", "main", transport, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(second.Hex, Git(remote, "rev-parse", "main").Trim());
        Assert.Equal("segundo\nprimeiro", Git(remote, "log", "--format=%s", "main").Trim().Replace("\r", ""));
        Git(remote, "fsck", "--strict");
    }

    [Fact]
    public async Task Clone_AndFetch_ReadPacksWithDeltasFromRealGit()
    {
        var remote = BareRemote();
        var seed = Dir("seed");
        Git(seed, "init", "-q", "-b", "main");
        var big = string.Join('\n', Enumerable.Range(0, 500).Select(i => $"linha {i} de um arquivo grande para gerar deltas de verdade"));
        Write(seed, "big.txt", big + "\n");
        Write(seed, "src/a.cs", "class A { }\n");
        Git(seed, "add", "-A");
        Git(seed, "commit", "-q", "-m", "um");
        Write(seed, "big.txt", big.Replace("linha 250 ", "LINHA 250 ") + "\nfim\n");
        Git(seed, "commit", "-q", "-am", "dois");
        Git(seed, "branch", "feature");
        Git(seed, "push", "-q", remote, "main", "feature");
        Git(remote, "gc", "-q", "--aggressive");

        var transport = new ProcessGitTransport(remote);
        var target = Path.Combine(_root, "clone");
        var clone = await GitRepository.CloneAsync("https://example.com/x.git", target, transport, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal("main", clone.CurrentBranch);
        Assert.Equal(["dois", "um"], clone.Log().Select(c => c.Summary));
        Assert.Contains("LINHA 250 ", File.ReadAllText(Path.Combine(target, "big.txt")));
        Assert.Empty(clone.GetStatus());
        Git(target, "fsck", "--strict");
        Assert.Equal("", Git(target, "status", "--porcelain").Trim());
        Assert.Contains(clone.Branches(includeRemote: true), b => b.Name == "origin/feature");

        // Novo commit no servidor (feito com o git de verdade) → fetch traz só o que falta.
        Write(seed, "novo.txt", "novo\n");
        Git(seed, "add", "-A");
        Git(seed, "commit", "-q", "-m", "tres");
        Git(seed, "push", "-q", remote, "main");
        var fetched = await clone.FetchAsync("origin", transport, cancellation: TestContext.Current.CancellationToken);
        Assert.Contains("origin/main", fetched.UpdatedRefs);
        Assert.True(fetched.ObjectsReceived > 0);
        Assert.Equal(MergeOutcome.FastForward, clone.Merge(clone.ResolveRef("refs/remotes/origin/main")!.Value, Me));
        Assert.Equal("novo\n", File.ReadAllText(Path.Combine(target, "novo.txt")));
    }

    [Fact]
    public async Task Pull_MergesDivergedWork_AndPushIsRefusedUntilPulled()
    {
        var remote = BareRemote();
        var transport = new ProcessGitTransport(remote);
        var seed = Dir("seed");
        Git(seed, "init", "-q", "-b", "main");
        Write(seed, "a.txt", "a\n");
        Write(seed, "b.txt", "b\n");
        Git(seed, "add", "-A");
        Git(seed, "commit", "-q", "-m", "base");
        Git(seed, "push", "-q", remote, "main");

        var target = Path.Combine(_root, "mine");
        var repo = await GitRepository.CloneAsync("https://example.com/x.git", target, transport, cancellation: TestContext.Current.CancellationToken);

        // Servidor recebe um commit por outro cliente; eu também faço um commit local.
        Write(seed, "a.txt", "a do servidor\n");
        Git(seed, "commit", "-q", "-am", "servidor muda a");
        Git(seed, "push", "-q", remote, "main");
        Write(target, "b.txt", "b meu\n");
        repo.StageAll();
        repo.Commit("eu mudo b", Me);

        var refused = await Assert.ThrowsAsync<GitException>(() => repo.PushAsync("origin", "main", transport, cancellation: TestContext.Current.CancellationToken));
        Assert.Contains("pull", refused.Message);

        var outcome = await repo.PullAsync("origin", transport, Me, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(MergeOutcome.Merged, outcome);
        Assert.Equal("a do servidor\n", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.Equal("b meu\n", File.ReadAllText(Path.Combine(target, "b.txt")));
        Git(target, "fsck", "--strict");

        await repo.PushAsync("origin", "main", transport, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(repo.Head!.Value.Hex, Git(remote, "rev-parse", "main").Trim());
        Assert.Equal(2, Git(remote, "log", "-1", "--format=%p", "main").Trim().Split(' ').Length);
    }

    [Fact]
    public async Task Advertisement_ParsesRefsCapabilitiesAndHeadTarget_FromRealGit()
    {
        var remote = BareRemote();
        var seed = Dir("seed");
        Git(seed, "init", "-q", "-b", "main");
        Write(seed, "a.txt", "a\n");
        Git(seed, "add", "-A");
        Git(seed, "commit", "-q", "-m", "x");
        Git(seed, "push", "-q", remote, "main");
        var advertisement = Advertisement.Parse(await new ProcessGitTransport(remote).AdvertiseAsync("git-upload-pack", TestContext.Current.CancellationToken));
        Assert.Contains("refs/heads/main", advertisement.Refs.Keys);
        Assert.Contains("side-band-64k", advertisement.Capabilities);
        Assert.Equal("refs/heads/main", advertisement.HeadTarget);
    }

    [Fact]
    public void PackFormat_RoundTrips_AndDetectsCorruption()
    {
        var store = new ObjectStore(Path.Combine(_root, "objects"));
        var blob = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("conteúdo ", 5000)));
        var pack = PackFormat.Build([(ObjectType.Blob, blob), (ObjectType.Blob, "pequeno"u8.ToArray())]);
        Assert.Equal(2, PackFormat.Unpack(pack, store));
        Assert.Equal(blob, store.Read(ObjectId.Compute(ObjectType.Blob, blob)).Data);
        var corrupted = (byte[])pack.Clone();
        corrupted[20] ^= 0xFF;
        Assert.Throws<GitException>(() => PackFormat.Unpack(corrupted, store));
    }
}
