using System.Text;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using Lunet.Ai;

namespace Lunet.Android;

/// <summary>
/// SPEC §16: API keys live encrypted with an AES-GCM key that never leaves the Android Keystore.
/// Only ciphertext (IV + data) is written to app-private storage; never to the project, Git or exported APKs.
/// </summary>
internal sealed class AndroidKeystoreVault(string directory) : ISecretVault
{
    private const string Alias = "lunet.secrets.v1";
    private const string Provider = "AndroidKeyStore";

    public string? Get(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return null;
        try
        {
            var blob = Convert.FromBase64String(File.ReadAllText(path));
            if (blob.Length < 13) return null;
            var iv = blob[..12];
            var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(CipherMode.DecryptMode, Key(), new GCMParameterSpec(128, iv));
            return Encoding.UTF8.GetString(cipher.DoFinal(blob[12..])!);
        }
        catch (Exception)
        {
            // Key invalidated (e.g. device credential reset) or corrupt file: treat as not configured.
            return null;
        }
    }

    public void Set(string name, string value)
    {
        var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(CipherMode.EncryptMode, Key());
        var iv = cipher.GetIV()!;
        var data = cipher.DoFinal(Encoding.UTF8.GetBytes(value))!;
        Directory.CreateDirectory(directory);
        var path = PathFor(name);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, Convert.ToBase64String([.. iv, .. data]));
        File.Move(temporary, path, overwrite: true);
    }

    public void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
    }

    private string PathFor(string name) =>
        System.IO.Path.Combine(directory, Convert.ToHexString(Encoding.UTF8.GetBytes(name)).ToLowerInvariant() + ".bin");

    private static IKey Key()
    {
        var store = KeyStore.GetInstance(Provider)!;
        store.Load(null);
        if (store.GetKey(Alias, null) is { } existing) return existing;
        var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, Provider)!;
        generator.Init(new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)!
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)!
            .SetKeySize(256)!
            .Build());
        return generator.GenerateKey()!;
    }
}
