using Android.Runtime;
using Android.Security.Keystore;
using Java.Security;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// Stable, individual per-game signing key in Android Keystore; never exports or stores the private key.
/// A Lunet reinstall erases this app's Keystore entry; previously installed games then cannot be updated
/// with the same certificate without an exported backup (not supported in this beta).
/// </summary>
internal sealed class AndroidGameApkSigner : IGameApkSigner
{
    private readonly string _alias;
    public byte[] CertificateDer { get; }
    public byte[] PublicKeyDer { get; }

    public AndroidGameApkSigner(Guid gameId)
    {
        _alias = "lunet-game-" + gameId.ToString("N");
        using var keyStore = KeyStore.GetInstance("AndroidKeyStore")
            ?? throw new InvalidOperationException("Android Keystore indisponível.");
        keyStore.Load(null);
        if (!keyStore.ContainsAlias(_alias))
        {
            using var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmRsa, "AndroidKeyStore")
                ?? throw new InvalidOperationException("RSA Android Keystore indisponível.");
            using var spec = new KeyGenParameterSpec.Builder(_alias, KeyStorePurpose.Sign | KeyStorePurpose.Verify)
                .SetKeySize(2048)
                .SetDigests(KeyProperties.DigestSha256)
                .SetSignaturePaddings(KeyProperties.SignaturePaddingRsaPkcs1)
                .Build();
            generator.Initialize(spec);
            using var pair = generator.GenerateKeyPair();
        }
        using var certificate = keyStore.GetCertificate(_alias)
            ?? throw new InvalidOperationException("Certificado do jogo não disponível.");
        CertificateDer = certificate.GetEncoded()
            ?? throw new InvalidOperationException("Certificado do jogo inválido.");
        PublicKeyDer = certificate.PublicKey?.GetEncoded()
            ?? throw new InvalidOperationException("Chave pública do jogo inválida.");
    }

    public byte[] Sign(byte[] signedData)
    {
        using var keyStore = KeyStore.GetInstance("AndroidKeyStore")
            ?? throw new InvalidOperationException("Android Keystore indisponível.");
        keyStore.Load(null);
        using var key = keyStore.GetKey(_alias, null)
            ?? throw new InvalidOperationException("Chave de assinatura do jogo perdida.");
        using var signature = Signature.GetInstance("SHA256withRSA")
            ?? throw new InvalidOperationException("Algoritmo SHA256withRSA indisponível.");
        // GetKey returns a JNI IKey proxy, not necessarily a managed IPrivateKey proxy.
        // A C# downcast may throw InvalidCastException even for a valid Java private key.
        // JavaCast checks the Java type and supplies the correctly typed Android binding.
        var privateKey = key.JavaCast<IPrivateKey>()
            ?? throw new InvalidOperationException("A chave do jogo não é uma chave privada RSA.");
        signature.InitSign(privateKey);
        signature.Update(signedData);
        var signatureBytes = signature.Sign()
            ?? throw new InvalidOperationException("Android Keystore não produziu assinatura.");

        // On-device cryptographic smoke test against the stored certificate. A signing
        // failure must never silently produce a seemingly successful, un-installable APK.
        using var certificate = keyStore.GetCertificate(_alias)
            ?? throw new InvalidOperationException("Certificado de assinatura do jogo perdido.");
        using var verifier = Signature.GetInstance("SHA256withRSA")
            ?? throw new InvalidOperationException("Verificador RSA indisponível.");
        verifier.InitVerify(certificate.PublicKey
            ?? throw new InvalidOperationException("Certificado sem chave pública."));
        verifier.Update(signedData);
        if (!verifier.Verify(signatureBytes))
            throw new InvalidOperationException("A assinatura do jogo não corresponde ao certificado salvo.");
        return signatureBytes;
    }
}
