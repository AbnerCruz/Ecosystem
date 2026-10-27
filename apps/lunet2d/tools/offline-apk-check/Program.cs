using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO.Compression;
using Lunet.Core;

// CI-only host smoke: production uses Android Keystore, never this temporary key.
if (args.Length is < 3 or > 5)
    throw new ArgumentException("Usage: offline-apk-check TEMPLATE GAME_DLL RESULT_APK [PACKAGE_ID] [portrait|landscape]");
using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=Lunet APK Offline Smoke", rsa,
    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
var signer = new SmokeSigner(rsa, certificate);
var files = new Dictionary<string, byte[]>
{
    ["game/Game.dll"] = File.ReadAllBytes(args[1]),
    ["game/Content/Data/check.txt"] = "offline build"u8.ToArray()
};
string packageId = args.Length >= 4 ? args[3] : "io.lunet.game.offline.smoketest";
string orientation = args.Length >= 5 ? args[4] : "portrait";
var apk = OfflineGameApkBuilder.Build(File.ReadAllBytes(args[0]),
    files, packageId, "Offline smoke", signer, orientation);
File.WriteAllBytes(args[2], apk);
using var archive = ZipFile.OpenRead(args[2]);
var game = archive.GetEntry("assets/game/Game.dll") ?? throw new Exception("Game.dll não empacotado.");
using var content = archive.GetEntry("assets/game/Content/Data/check.txt")?.Open() ?? throw new Exception("Content não empacotado.");
if (game.Length != files["game/Game.dll"].Length) throw new Exception("Game.dll incompleto.");
Console.WriteLine($"Offline game APK: {apk.Length} bytes; PackageId={packageId}; orientation={orientation}; v2 signing; 2 assets.");

internal sealed class SmokeSigner(RSA rsa, X509Certificate2 certificate) : IGameApkSigner
{
    public byte[] CertificateDer => certificate.RawData;
    public byte[] PublicKeyDer => rsa.ExportSubjectPublicKeyInfo();
    public byte[] Sign(byte[] signedData) => rsa.SignData(signedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
}
