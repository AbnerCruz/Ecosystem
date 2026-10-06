using System.Text;
using Urbe.Core;
namespace Urbe.Core.Tests;
public sealed class ImportTransactionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact] public async Task RealFilesystemTransactionIsBinarySafeAndReimportDoesNotDuplicate()
    {
        await using var storage = new DiskStorage();var input=await Inspect("ação.bin",[0,255,137]);var snapshot=VaultReader.Read([]);
        var plan=ImportPipeline.Plan(input,snapshot,ImportTransaction.Revision(snapshot.Files.Values));var result=await ImportPipeline.ExecuteAsync(plan,new ImportTransaction(storage),Token);
        Assert.Equal(1,result.Written);Assert.Equal(new byte[]{0,255,137},(await storage.ReadAsync("ação.bin",Token))!.Value.ToArray());
        snapshot=await Snapshot(storage);plan=ImportPipeline.Plan(input,snapshot,ImportTransaction.Revision(snapshot.Files.Values));result=await ImportPipeline.ExecuteAsync(plan,new ImportTransaction(storage),Token);Assert.Equal(0,result.Written);Assert.Equal(1,result.Unchanged);
    }
    [Fact] public async Task IoFailureDuringApplicationRollsBackAndLeavesOriginalBytes()
    {
        await using var storage=new DiskStorage();await storage.WriteAsync("A.md",Encoding.UTF8.GetBytes("original"),Token);var input=await Inspect("A.md",Encoding.UTF8.GetBytes("new"),"B.md",Encoding.UTF8.GetBytes("second"));var snapshot=await Snapshot(storage);
        var plan=ImportPipeline.Plan(input,snapshot,ImportTransaction.Revision(snapshot.Files.Values),new Dictionary<string,ImportConflictChoice>{{"A.md",ImportConflictChoice.Replace}});storage.FailAt="B.md";
        await Assert.ThrowsAsync<IOException>(()=>ImportPipeline.ExecuteAsync(plan,new ImportTransaction(storage),Token).AsTask());Assert.Equal("original",Encoding.UTF8.GetString((await storage.ReadAsync("A.md",Token))!.Value.Span));Assert.Null(await storage.ReadAsync("B.md",Token));Assert.Null(await storage.ReadAsync(ImportTransaction.JournalPath,Token));
    }
    [Fact] public async Task FailedRollbackSurvivesReopeningAndRecoversBeforeNextImport()
    {
        await using var storage=new DiskStorage();await storage.WriteAsync("A.md",Encoding.UTF8.GetBytes("original"),Token);var input=await Inspect("A.md",Encoding.UTF8.GetBytes("new"),"B.md",Encoding.UTF8.GetBytes("second"));var snapshot=await Snapshot(storage);
        var plan=ImportPipeline.Plan(input,snapshot,ImportTransaction.Revision(snapshot.Files.Values),new Dictionary<string,ImportConflictChoice>{{"A.md",ImportConflictChoice.Replace}});storage.FailAt="B.md";storage.FailRollback=true;
        await Assert.ThrowsAsync<IOException>(()=>ImportPipeline.ExecuteAsync(plan,new ImportTransaction(storage),Token).AsTask());Assert.NotNull(await storage.ReadAsync(ImportTransaction.JournalPath,Token));
        var reopened=new ImportTransaction(storage);Assert.True(await reopened.RecoverAsync(Token));Assert.Equal("original",Encoding.UTF8.GetString((await storage.ReadAsync("A.md",Token))!.Value.Span));Assert.False(await reopened.RecoverAsync(Token));
    }
    [Fact] public async Task StaleRevisionAndCancelNeverChangeCurrentVault()
    {
        await using var storage=new DiskStorage();var input=await Inspect("A.md",[65]);var plan=ImportPipeline.Plan(input,VaultReader.Read([]),ImportTransaction.Revision([]));await storage.WriteAsync("outside.md",new byte[]{66},Token);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>ImportPipeline.ExecuteAsync(plan,new ImportTransaction(storage),Token).AsTask());Assert.Null(await storage.ReadAsync("A.md",Token));
        using var canceled=new CancellationTokenSource();canceled.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ImportPipeline.ExecuteAsync(plan,new ImportTransaction(storage),canceled.Token).AsTask());
    }
    private static ValueTask<ImportInspection> Inspect(string path,byte[] bytes,string? second=null,byte[]? more=null)
    {
        var sources=new List<ImportSource>{new(path,_=>ValueTask.FromResult<Stream>(new MemoryStream(bytes,false)))};if(second is not null)sources.Add(new(second,_=>ValueTask.FromResult<Stream>(new MemoryStream(more!,false))));return ImportPipeline.InspectAsync(new ImportSelection(sources),cancellationToken:TestContext.Current.CancellationToken);
    }
    private static async Task<VaultSnapshot> Snapshot(DiskStorage storage){var files=new List<VaultFile>();foreach(var path in await storage.ListAsync(TestContext.Current.CancellationToken))files.Add(new VaultFile(path,(await storage.ReadAsync(path,TestContext.Current.CancellationToken))!.Value.ToArray()));return VaultReader.Read(files);}
    // Real disk IO with faults at the host boundary, not a mock importer or an expected-value implementation.
    private sealed class DiskStorage:IImportStorage,IAsyncDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"urbe-import-test-"+Guid.NewGuid());private readonly SemaphoreSlim gate=new(1);public string? FailAt;public bool FailRollback;private bool failed;
        public DiskStorage()=>Directory.CreateDirectory(root);
        public async ValueTask<IAsyncDisposable> AcquireExclusiveAsync(CancellationToken token){await gate.WaitAsync(token);return new Release(gate);}
        public ValueTask<IReadOnlyList<string>> ListAsync(CancellationToken token){token.ThrowIfCancellationRequested();return ValueTask.FromResult<IReadOnlyList<string>>(Directory.GetFiles(root,"*",SearchOption.AllDirectories).Select(p=>Path.GetRelativePath(root,p).Replace('\\','/')).ToArray());}
        public async ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string path,CancellationToken token){var full=Path.Combine(root,path);return File.Exists(full)?new ReadOnlyMemory<byte>(await File.ReadAllBytesAsync(full,token)):(ReadOnlyMemory<byte>?)null;}
        public async ValueTask WriteAsync(string path,ReadOnlyMemory<byte> bytes,CancellationToken token){if(path==FailAt){FailAt=null;failed=true;throw new IOException("ENOSPC at host IO boundary");}if(failed&&FailRollback&&path=="A.md"){FailRollback=false;throw new IOException("device unavailable during rollback");}var full=Path.Combine(root,path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);await File.WriteAllBytesAsync(full,bytes.ToArray(),token);}
        public ValueTask RemoveAsync(string path,CancellationToken token){token.ThrowIfCancellationRequested();File.Delete(Path.Combine(root,path));return ValueTask.CompletedTask;}
        public ValueTask DisposeAsync(){Directory.Delete(root,true);gate.Dispose();return ValueTask.CompletedTask;}
        private sealed class Release(SemaphoreSlim gate):IAsyncDisposable{public ValueTask DisposeAsync(){gate.Release();return ValueTask.CompletedTask;}}
    }
}
