using System.Diagnostics;
using System.IO.Compression;
using Lunet.Git;

namespace Lunet.Tests;

public sealed class GitTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-git-" + Guid.NewGuid().ToString("N"));
    private static readonly GitSignature Me = new("Ana", "ana@example.com", new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.FromHours(-3)));

    public GitTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private string Dir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Write(string dir, string relative, string text)
    {
        var path = Path.Combine(dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string RunGit(string dir, params string[] arguments)
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

    [Fact]
    public void Inflate_MatchesZLibStream_ForAllCompressionLevels_AndReportsConsumedBytes()
    {
        var random = new Random(5);
        var samples = new[]
        {
            "hello hello hello hello"u8.ToArray(),
            Enumerable.Range(0, 100_000).Select(i => (byte)(i * 7 % 251)).ToArray(),
            Enumerable.Range(0, 50_000).Select(_ => (byte)random.Next(256)).ToArray(),
            System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("public class Player : Game { int Score; }\n", 2000))),
        };
        foreach (var sample in samples)
        {
            foreach (var level in new[] { CompressionLevel.NoCompression, CompressionLevel.Fastest, CompressionLevel.Optimal, CompressionLevel.SmallestSize })
            {
                using var compressed = new MemoryStream();
                using (var zlib = new ZLibStream(compressed, level, leaveOpen: true)) zlib.Write(sample);
                var packed = compressed.ToArray();
                var withTail = packed.Concat(new byte[] { 1, 2, 3, 4, 5 }).ToArray();
                var (data, consumed) = Inflate.Zlib(withTail, 0, sample.Length);
                Assert.Equal(sample, data);
                Assert.Equal(packed.Length, consumed);
            }
        }
    }

    [Fact]
    public void Init_StageCommit_ProduceARepositoryThatRealGitAccepts()
    {
        var dir = Dir("mine");
        var repo = GitRepository.Init(dir);
        Write(dir, "Game.cs", "class A { }\n");
        Write(dir, "Content/data.json", "{ \"a\": 1 }\n");
        Write(dir, ".gitignore", ".lunet/\nbin/\n");
        Write(dir, ".lunet/autosave/x.cs", "ignorado");
        repo.StageAll();
        var first = repo.Commit("Primeiro commit", Me);

        Assert.Equal("main", repo.CurrentBranch);
        Assert.Equal(first, repo.Head);
        Assert.DoesNotContain(repo.GetStatus(), s => s.Path.StartsWith(".lunet", StringComparison.Ordinal));
        Assert.Empty(repo.GetStatus());

        Assert.Equal(first.Hex, RunGit(dir, "rev-parse", "HEAD").Trim());
        RunGit(dir, "fsck", "--strict");
        Assert.Equal("", RunGit(dir, "status", "--porcelain").Trim());
        Assert.Contains("Ana <ana@example.com>", RunGit(dir, "log", "-1", "--format=%an <%ae>"));
        Assert.Equal(".gitignore\nContent/data.json\nGame.cs", RunGit(dir, "ls-files").Trim().Replace("\r", ""));

        // Segundo commit com modificação e arquivo novo.
        Write(dir, "Game.cs", "class A { int x; }\n");
        Write(dir, "Novo.cs", "class B { }\n");
        var status = repo.GetStatus().ToDictionary(s => s.Path);
        Assert.Equal(FileChange.Modified, status["Game.cs"].Unstaged);
        Assert.Equal(FileChange.Untracked, status["Novo.cs"].Unstaged);
        repo.Stage("Game.cs");
        Assert.Equal(FileChange.Modified, repo.GetStatus().First(s => s.Path == "Game.cs").Staged);
        var second = repo.Commit("Muda Game", Me);
        Assert.Equal([second, first], repo.Log().Select(c => c.Id));
        RunGit(dir, "fsck", "--strict");
        Assert.Equal("?? Novo.cs", RunGit(dir, "status", "--porcelain").Trim());
    }

    [Fact]
    public void ReadsRepositoriesCreatedByRealGit_IncludingPackedObjectsWithDeltas()
    {
        var dir = Dir("real");
        RunGit(dir, "init", "-q", "-b", "main");
        var big = string.Join('\n', Enumerable.Range(0, 400).Select(i => $"linha {i} de um arquivo grande para gerar deltas"));
        Write(dir, "big.txt", big + "\n");
        Write(dir, "src/a.cs", "class A { }\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-q", "-m", "um");
        Write(dir, "big.txt", big.Replace("linha 200 ", "LINHA 200 ") + "\nfim\n");
        RunGit(dir, "commit", "-q", "-am", "dois");
        RunGit(dir, "branch", "feature");
        RunGit(dir, "gc", "-q", "--aggressive");

        var repo = GitRepository.Open(dir);
        var log = repo.Log();
        Assert.Equal(["dois", "um"], log.Select(c => c.Summary));
        Assert.Equal("Bia", log[0].Author.Name);
        Assert.Equal(["feature", "main"], repo.Branches().Select(b => b.Name).Order());
        Assert.Equal("main", repo.CurrentBranch);
        Assert.Empty(repo.GetStatus());

        var files = repo.Flatten(log[0].Tree);
        Assert.Equal(["big.txt", "src/a.cs"], files.Keys.Order());
        var blob = repo.Objects.Read(files["big.txt"].Id);
        Assert.Contains("LINHA 200 ", System.Text.Encoding.UTF8.GetString(blob.Data));
        Assert.Contains("+fim", repo.DiffCommitFile(log[0].Id, "big.txt"));
        Assert.Equal([new FileDiffInfo("big.txt", FileChange.Modified)], repo.CommitChanges(log[0].Id));
    }

    [Fact]
    public void Branches_CheckoutMoveTheWorkingTree_AndRefuseToLoseLocalChanges()
    {
        var dir = Dir("branches");
        var repo = GitRepository.Init(dir);
        Write(dir, "a.txt", "um\n");
        Write(dir, "b.txt", "b\n");
        repo.StageAll();
        var c1 = repo.Commit("c1", Me);

        repo.CreateBranch("feature");
        repo.Checkout("feature");
        Assert.Equal("feature", repo.CurrentBranch);
        Write(dir, "a.txt", "dois\n");
        Write(dir, "c/novo.txt", "novo\n");
        File.Delete(Path.Combine(dir, "b.txt"));
        repo.StageAll();
        var c2 = repo.Commit("c2", Me);

        repo.Checkout("main");
        Assert.Equal("um\n", File.ReadAllText(Path.Combine(dir, "a.txt")));
        Assert.True(File.Exists(Path.Combine(dir, "b.txt")));
        Assert.False(Directory.Exists(Path.Combine(dir, "c")));
        Assert.Empty(repo.GetStatus());
        RunGit(dir, "fsck", "--strict");
        Assert.Equal("", RunGit(dir, "status", "--porcelain").Trim());

        Write(dir, "a.txt", "alterado localmente\n");
        var refused = Assert.Throws<GitException>(() => repo.Checkout("feature"));
        Assert.Contains("a.txt", refused.Message);
        Assert.Equal("main", repo.CurrentBranch);
        Assert.Equal("alterado localmente\n", File.ReadAllText(Path.Combine(dir, "a.txt")));

        repo.DiscardChanges("a.txt");
        Assert.Equal("um\n", File.ReadAllText(Path.Combine(dir, "a.txt")));
        repo.Checkout("feature");
        Assert.Equal(c2, repo.Head);
        Assert.Equal("dois\n", File.ReadAllText(Path.Combine(dir, "a.txt")));

        repo.Checkout(c1.Hex);
        Assert.Null(repo.CurrentBranch);
        Assert.Equal(c1, repo.Head);

        Assert.Throws<GitException>(() => repo.CreateBranch("feature"));
        Assert.Throws<GitException>(() => repo.CreateBranch("nome inválido"));
        repo.Checkout("main");
        Assert.Throws<GitException>(() => repo.DeleteBranch("feature")); // não mesclado
        repo.DeleteBranch("feature", force: true);
        Assert.DoesNotContain(repo.Branches(), b => b.Name == "feature");
    }

    [Fact]
    public void Revert_UndoesACommit_AndRefusesWhenLaterChangesConflict()
    {
        var dir = Dir("revert");
        var repo = GitRepository.Init(dir);
        Write(dir, "a.txt", "1\n");
        repo.StageAll();
        repo.Commit("c1", Me);
        Write(dir, "a.txt", "2\n");
        Write(dir, "novo.txt", "n\n");
        repo.StageAll();
        var c2 = repo.Commit("c2", Me);

        var revert = repo.Revert(c2, Me);
        Assert.Equal("1\n", File.ReadAllText(Path.Combine(dir, "a.txt")));
        Assert.False(File.Exists(Path.Combine(dir, "novo.txt")));
        Assert.StartsWith("Reverter \"c2\"", repo.GetCommit(revert).Message);
        RunGit(dir, "fsck", "--strict");
        Assert.Equal("", RunGit(dir, "status", "--porcelain").Trim());

        Write(dir, "a.txt", "3\n");
        repo.StageAll();
        var c4 = repo.Commit("c4", Me);
        var conflict = Assert.Throws<GitException>(() => repo.Revert(c2, Me));
        Assert.Contains("a.txt", conflict.Message);
        Write(dir, "sujo.txt", "x");
        repo.Stage("sujo.txt");
        Assert.Throws<GitException>(() => repo.Revert(c4, Me)); // há mudança preparada
    }

    [Fact]
    public void Merge_FastForwardsOrMergesFilesWithoutConflicts_AndReportsConflicts()
    {
        var dir = Dir("merge");
        var repo = GitRepository.Init(dir);
        Write(dir, "a.txt", "a\n");
        Write(dir, "b.txt", "b\n");
        repo.StageAll();
        repo.Commit("base", Me);

        repo.CheckoutNewBranch("feature");
        Write(dir, "a.txt", "a2\n");
        repo.StageAll();
        var featureTip = repo.Commit("feature muda a", Me);

        repo.Checkout("main");
        Assert.Equal(MergeOutcome.FastForward, repo.Merge(featureTip, Me));
        Assert.Equal(featureTip, repo.Head);
        Assert.Equal(MergeOutcome.UpToDate, repo.Merge(featureTip, Me));

        repo.Checkout("feature");
        Write(dir, "c.txt", "c\n");
        repo.StageAll();
        var other = repo.Commit("feature adiciona c", Me);
        repo.Checkout("main");
        Write(dir, "b.txt", "b2\n");
        repo.StageAll();
        repo.Commit("main muda b", Me);
        Assert.Equal(MergeOutcome.Merged, repo.Merge(other, Me));
        Assert.Equal(2, repo.GetCommit(repo.Head!.Value).Parents.Count);
        Assert.Equal("b2\n", File.ReadAllText(Path.Combine(dir, "b.txt")));
        Assert.Equal("c\n", File.ReadAllText(Path.Combine(dir, "c.txt")));
        RunGit(dir, "fsck", "--strict");
        Assert.Equal("", RunGit(dir, "status", "--porcelain").Trim());

        repo.CheckoutNewBranch("x");
        Write(dir, "a.txt", "de x\n");
        repo.StageAll();
        var x = repo.Commit("x muda a", Me);
        repo.Checkout("main");
        Write(dir, "a.txt", "de main\n");
        repo.StageAll();
        repo.Commit("main muda a", Me);
        var conflict = Assert.Throws<GitException>(() => repo.Merge(x, Me));
        Assert.Contains("a.txt", conflict.Message);
    }

    [Fact]
    public void Diff_ProducesUnifiedHunksLikeGit()
    {
        var text = LineDiff.Unified("a/f", "b/f", "1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n", "1\n2\n3\n4\nCINCO\n6\n7\n8\n9\n10\n11\n");
        Assert.Equal("--- a/f\n+++ b/f\n@@ -2,9 +2,10 @@\n 2\n 3\n 4\n-5\n+CINCO\n 6\n 7\n 8\n 9\n 10\n+11\n", text);
        Assert.Equal("", LineDiff.Unified("a", "b", "x\n", "x\n"));
        Assert.Contains("\\ No newline at end of file", LineDiff.Unified("a", "b", "x", "x\ny\n"));
    }

    [Fact]
    public void Diff_AgreesWithRealGit_OnRandomEdits()
    {
        var random = new Random(11);
        var dir = Dir("diff");
        RunGit(dir, "init", "-q");
        for (var round = 0; round < 6; round++)
        {
            var oldLines = Enumerable.Range(0, 60).Select(i => $"linha {i}").ToList();
            var newLines = oldLines.ToList();
            for (var edit = 0; edit < 5; edit++)
            {
                var at = random.Next(newLines.Count);
                switch (random.Next(3))
                {
                    case 0: newLines.RemoveAt(at); break;
                    case 1: newLines.Insert(at, "nova " + edit); break;
                    default: newLines[at] = "mudou " + edit; break;
                }
            }
            var oldText = string.Join('\n', oldLines) + "\n";
            var newText = string.Join('\n', newLines) + "\n";
            Write(dir, "old.txt", oldText);
            Write(dir, "new.txt", newText);
            var real = RunGitAllowDiff(dir, "diff", "--no-index", "--no-color", "old.txt", "new.txt");
            var mine = LineDiff.Unified("a/old.txt", "b/new.txt", oldText, newText);
            var realHunks = string.Join('\n', real.Split('\n').Where(l => l.StartsWith('@') || l.StartsWith('+') && !l.StartsWith("+++") || l.StartsWith('-') && !l.StartsWith("---")));
            var mineHunks = string.Join('\n', mine.Split('\n').Where(l => l.StartsWith('@') || l.StartsWith('+') && !l.StartsWith("+++") || l.StartsWith('-') && !l.StartsWith("---")));
            // Os mesmos trechos alterados; o cabeçalho @@ pode variar quando o Git omite ",1".
            static string Strip(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"@@ .* @@.*", "@@").Replace(",1 ", " ");
            Assert.Equal(Strip(realHunks).Split('\n').Where(l => l != "@@"), Strip(mineHunks).Split('\n').Where(l => l != "@@"));
        }
    }

    private static string RunGitAllowDiff(string dir, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    [Fact]
    public void Config_ReadsAndWritesRemotesAndUser()
    {
        var dir = Dir("config");
        var repo = GitRepository.Init(dir);
        var config = repo.Config;
        config.Set("remote", "origin", "url", "https://github.com/AbnerCruz/Lunet2D.git");
        config.Set("user", null, "name", "Ana");
        config.Save();
        var again = repo.Config;
        Assert.Equal("https://github.com/AbnerCruz/Lunet2D.git", again.Get("remote", "origin", "url"));
        Assert.Equal("Ana", again.Get("user", null, "name"));
        Assert.Contains("origin", again.Subsections("remote"));
        Assert.Equal("https://github.com/AbnerCruz/Lunet2D.git", RunGit(dir, "config", "remote.origin.url").Trim());
    }
}
