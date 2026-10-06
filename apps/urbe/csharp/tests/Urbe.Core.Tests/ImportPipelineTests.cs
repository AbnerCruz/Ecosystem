using System.Text;
using Urbe.Core;
namespace Urbe.Core.Tests;
public sealed class ImportPipelineTests
{
    private static ValueTask<ImportInspection> Inspect(ImportSelection selection,ImportLimits? limits=null)=>ImportPipeline.InspectAsync(selection,limits,TestContext.Current.CancellationToken);
    private static ImportSource Text(string path,string value)=>Source(path,Encoding.UTF8.GetBytes(value));
    private static ImportSource Source(string path,byte[] bytes)=>new(path,_=>ValueTask.FromResult<Stream>(new NonSeekable(bytes)));
    private static ImportSelection Select(params ImportSource[] sources)=>new(sources);
    [Fact] public async Task MultipleUnicodeMarkdownAndBinaryAssetsUseNonSeekableStreams()
    {
        var x=await Inspect(Select(Text("ação 🙂.md","![[foto.png]]"),Text("B.md","# B"),Source("foto.png",[137,80,0,255])));
        Assert.Equal(ImportKind.Documents,x.Kind);Assert.Equal(2,x.DocumentCount);Assert.Equal(1,x.AssetCount);
        Assert.Equal(new byte[]{137,80,0,255},x.Files["foto.png"].Bytes.ToArray());Assert.Equal(3,ImportPipeline.Plan(x,VaultReader.Read([]),"rev").Operations.Count);
    }
    [Theory][InlineData(1,ImportKind.HistoricalVault)][InlineData(2,ImportKind.CurrentVault)]
    public async Task CurrentAndHistoricalVaultsPreserveMetadata(int version,ImportKind kind)
    {
        var x=await Inspect(Select(Text(".urbe/vault.json","{\"formatVersion\":"+version+"}"),Text("A.md","# A")));
        Assert.Equal(kind,x.Kind);Assert.True(x.Files.ContainsKey(".urbe/vault.json"));Assert.Equal(ImportMode.Restore,ImportPipeline.Plan(x,VaultReader.Read([]),"rev").Mode);
    }
    [Fact] public async Task HistoricalVaultWithoutEnvelopeIsDetected()=>Assert.Equal(ImportKind.HistoricalVault,(await Inspect(Select(Text(".urbe/mapa.json","{\"v\":1}"),Text("A.md","a")))).Kind);
    [Theory][InlineData(".urbe/vault.json","{\"formatVersion\":99}")][InlineData(".urbe/vault.json","{")][InlineData(".urbe/mapa.json","{\"v\":99}")][InlineData(".urbe/history.v2.json","{\"version\":99}")]
    public async Task FutureOrCorruptVaultIsRefused(string path,string value)=>await Assert.ThrowsAsync<InvalidDataException>(()=>Inspect(Select(Text(path,value),Text("A.md","a"))).AsTask());
    [Fact] public async Task ZipSignatureAndHiddenMetadataArePreserved()
    {
        var files=new[]{new VaultFile(".urbe/vault.json",Encoding.UTF8.GetBytes("{\"formatVersion\":2}")),new VaultFile("ação.md",Encoding.UTF8.GetBytes("# A")),new VaultFile("foto.bin",[0,255])};
        var zip=VaultArchive.Export(files);var x=await Inspect(Select(Source("anything.bin",zip.Bytes.ToArray())));Assert.Equal(ImportKind.CurrentVault,x.Kind);Assert.NotNull(x.Manifest);
        foreach(var f in files)Assert.Equal(f.Bytes.ToArray(),x.Files[f.Path].Bytes.ToArray());
    }
    [Fact] public async Task InvalidZipAndEncodingAreRefused(){await Assert.ThrowsAsync<InvalidDataException>(()=>Inspect(Select(Text("bad.zip","oops"))).AsTask());await Assert.ThrowsAsync<InvalidDataException>(()=>Inspect(Select(Source("bad.md",[255]))).AsTask());}
    [Fact] public async Task DuplicateSelectionCoalescesOnlyIdenticalBytes(){Assert.Single((await Inspect(Select(Text("A.md","a"),Text("a.md","a")))).Files);var conflict=await Inspect(Select(Text("A.md","a"),Text("a.md","b")));Assert.Single(conflict.SourceConflicts!);Assert.Throws<InvalidOperationException>(()=>ImportPipeline.Plan(conflict,VaultReader.Read([]),"rev"));var selected=ImportPipeline.ResolveSources(conflict,new Dictionary<string,int>{{"A.md",1}});Assert.Equal("b",Encoding.UTF8.GetString(selected.Files["a.md"].Bytes.Span));Assert.Single(ImportPipeline.Plan(selected,VaultReader.Read([]),"rev").Operations);}
    [Fact] public async Task ReimportIsIdempotentAndConflictsRequireChoice()
    {
        var current=VaultReader.Read([new VaultFile("A.md",Encoding.UTF8.GetBytes("a"))]);var same=await Inspect(Select(Text("A.md","a")));var again=ImportPipeline.Plan(same,current,"rev");Assert.Empty(again.Operations);Assert.Equal(1,again.Unchanged);
        var changed=await Inspect(Select(Text("A.md","b")));Assert.Single(ImportPipeline.Plan(changed,current,"rev").Conflicts);
        Assert.Empty(ImportPipeline.Plan(changed,current,"rev",new Dictionary<string,ImportConflictChoice>{{"A.md",ImportConflictChoice.KeepCurrent}}).Operations);
        Assert.Single(ImportPipeline.Plan(changed,current,"rev",new Dictionary<string,ImportConflictChoice>{{"A.md",ImportConflictChoice.Replace}}).Operations);Assert.Equal("a",Encoding.UTF8.GetString(current.Files["A.md"].Bytes.Span));
    }
    [Theory][InlineData("../evil.md")][InlineData("content://provider/doc")][InlineData("A\\B.md")][InlineData("/absolute.md")]
    public async Task UriAndTraversalAreNeverFilesystemNames(string path)=>await Assert.ThrowsAsync<InvalidDataException>(()=>Inspect(Select(Text(path,"a"))).AsTask());
    [Fact] public async Task CancellationAndProviderFailureDoNotProduceAPartialPlan()
    {
        using var token=new CancellationTokenSource();token.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ImportPipeline.InspectAsync(Select(Text("A.md","a")),cancellationToken:token.Token).AsTask());
        var failure=new ImportSource("A.md",_=>ValueTask.FromException<Stream>(new IOException("provider unavailable")));await Assert.ThrowsAsync<IOException>(()=>Inspect(Select(failure)).AsTask());
    }
    [Fact] public async Task LimitsApplyToStreamAndZipExpansion()
    {
        await Assert.ThrowsAsync<InvalidDataException>(()=>Inspect(Select(Text("large.md","12345")),new ImportLimits(10,4,10)).AsTask());
        var zip=VaultArchive.Export([new VaultFile("large.md",new byte[10000])]);Assert.Throws<InvalidDataException>(()=>VaultArchive.Import(zip.Bytes,new ImportLimits(20000,500,10)));
    }
    [Fact] public async Task ExtractedMigrationBackupOnlyRestoresCataloguedPaths()
    {
        var content="original 🙂";var bytes=Encoding.UTF8.GetBytes(content);var digest=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        var manifest=System.Text.Json.JsonSerializer.Serialize(new{version=1,from=1,to=2,files=new[]{new{path="A.md",size=content.Length,sha256=digest}},absent=new[]{"gone.md"}});
        var inspection=await Inspect(Select(Text("manifest.json",manifest),Source("files/A.md",bytes)));
        Assert.Equal(ImportKind.Backup,inspection.Kind);var current=VaultReader.Read([new VaultFile("unrelated.md",Encoding.UTF8.GetBytes("keep"))]);
        var plan=ImportPipeline.Plan(inspection,current,"rev");Assert.Equal(ImportMode.RestoreSubset,plan.Mode);Assert.DoesNotContain(plan.Operations,o=>o.Path=="unrelated.md");
    }
    [Fact] public async Task UnknownMetadataCannotTurnAnOrdinarySelectionIntoDestructiveVaultRestore()
        => await Assert.ThrowsAsync<InvalidDataException>(()=>Inspect(Select(Text(".urbe/unknown.json","{}"),Text("A.md","a"))).AsTask());
    [Fact] public async Task ZipAttachmentInsideSelectedFolderIsPreservedAsBinaryAsset()
    {
        var zip=VaultArchive.Export([new VaultFile("inside.md",Encoding.UTF8.GetBytes("inside"))]);
        var x=await Inspect(new ImportSelection([Text("A.md","note"),Source("assets/archive.zip",zip.Bytes.ToArray())],true));
        Assert.Equal(ImportKind.Documents,x.Kind);Assert.Equal(1,x.AssetCount);Assert.Equal(zip.Bytes.ToArray(),x.Files["assets/archive.zip"].Bytes.ToArray());Assert.False(x.Files.ContainsKey("inside.md"));
    }
    [Theory][InlineData("{\"version\":99}")][InlineData("{")]
    public async Task UnsupportedCandidateCannotHideBehindAnIdenticalPath(string value)
        => await Assert.ThrowsAsync<InvalidDataException>(()=>Inspect(Select(Text("A.page.json","{\"version\":1}"),Text("A.page.json",value))).AsTask());
    private sealed class NonSeekable(byte[] bytes):MemoryStream(bytes,false){public override bool CanSeek=>false;public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();}
}
