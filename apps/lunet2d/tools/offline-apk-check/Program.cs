using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO.Compression;
using Lunet.Core;

// CI-only host smoke: production uses Android Keystore, never this temporary key.
if (args.Length is < 3 or > 6)
    throw new ArgumentException("Usage: offline-apk-check TEMPLATE GAME_DLL RESULT_APK [PACKAGE_ID] [portrait|landscape] [PROJECT_ROOT]");
using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=Lunet APK Offline Smoke", rsa,
    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
var signer = new SmokeSigner(rsa, certificate);
var files = new Dictionary<string, byte[]>
{
    ["game/Game.dll"] = File.ReadAllBytes(args[1]),
};
if (args.Length == 6)
{
    // CI journey: feed the exact authored project into the production offline
    // packager. No fixtures are substituted for the project's Content/Assets/.
    string projectRoot = Path.GetFullPath(args[5]);
    if (!File.Exists(Path.Combine(projectRoot, "lunet.json")))
        throw new FileNotFoundException("Projeto Lunet sem lunet.json.", projectRoot);
    foreach (string folder in new[] { "Content", "Assets" })
    {
        string directory = Path.Combine(projectRoot, folder);
        if (!Directory.Exists(directory)) continue;
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Link simbólico não é permitido no teste: " + path);
            string relative = Path.GetRelativePath(projectRoot, path).Replace('\\', '/');
            if (relative.Split('/').Any(segment => segment is "" or "." or ".."))
                throw new InvalidDataException("Caminho inválido: " + relative);
            files.Add("game/" + relative, File.ReadAllBytes(path));
        }
    }
}
else
{
    files.Add("game/Content/Data/check.txt", "offline build"u8.ToArray());
}
string packageId = args.Length >= 4 ? args[3] : "io.lunet.game.offline.smoketest";
string orientation = args.Length >= 5 ? args[4] : "portrait";
var apk = OfflineGameApkBuilder.Build(File.ReadAllBytes(args[0]),
    files, packageId, "Offline smoke", signer, orientation);
File.WriteAllBytes(args[2], apk);
using var archive = ZipFile.OpenRead(args[2]);
var game = archive.GetEntry("assets/game/Game.dll") ?? throw new Exception("Game.dll não empacotado.");
if (game.Length != files["game/Game.dll"].Length) throw new Exception("Game.dll incompleto.");
foreach (var (relativePath, bytes) in files)
{
    var entry = archive.GetEntry("assets/" + relativePath)
        ?? throw new Exception("Recurso não empacotado: " + relativePath);
    if (entry.Length != bytes.Length)
        throw new Exception("Recurso truncado: " + relativePath);
    using var input = entry.Open();
    using var output = new MemoryStream();
    input.CopyTo(output);
    if (!output.ToArray().AsSpan().SequenceEqual(bytes))
        throw new Exception("Conteúdo divergente: " + relativePath);
}
Console.WriteLine($"Offline game APK: {apk.Length} bytes; PackageId={packageId}; orientation={orientation}; v2 signing; {files.Count} arquivos.");

internal sealed class SmokeSigner(RSA rsa, X509Certificate2 certificate) : IGameApkSigner
{
    public byte[] CertificateDer => certificate.RawData;
    public byte[] PublicKeyDer => rsa.ExportSubjectPublicKeyInfo();
    public byte[] Sign(byte[] signedData) => rsa.SignData(signedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
}
