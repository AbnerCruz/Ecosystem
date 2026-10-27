using System.IO;

namespace Lunet.Tests;

/// <summary>
/// Source-level Android interop regression contract. The Linux test runner cannot execute
/// AndroidKeyStore; the separate device matrix exercises the actual signing implementation.
/// </summary>
public sealed class AndroidGameApkSignerRegressionTests
{
    [Fact]
    public void KeystorePrivateKeyUsesJniCheckedCastNotManagedDowncast()
    {
        string source = ReadLunetSource("src/Lunet.Android/AndroidGameApkSigner.cs");
        Assert.Contains("JavaCast<IPrivateKey>()", source);
        Assert.DoesNotContain("(IPrivateKey)key", source);
    }

    [Fact]
    public void KeystoreSignerChecksGeneratedSignatureAgainstStoredCertificate()
    {
        string source = ReadLunetSource("src/Lunet.Android/AndroidGameApkSigner.cs");
        Assert.Contains("verifier.InitVerify(", source);
        Assert.Contains("verifier.Verify(", source);
    }

    [Fact]
    public void ExportErrorIdentifiesBuildStageInsteadOfOnlyGenericCastError()
    {
        string source = ReadLunetSource("src/Lunet.Android/MainActivity.GameBuild.cs");
        Assert.Contains("Etapa:", source);
        Assert.Contains("Android.Util.Log.Error(", source);
    }

    private static string ReadLunetSource(string path)
    {
        for (DirectoryInfo? cursor = new(AppContext.BaseDirectory);
             cursor is not null; cursor = cursor.Parent)
        {
            string candidate = Path.Combine(cursor.FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException($"Código-fonte do Lunet não encontrado: {path}");
    }
}
