using Android.Content;
using Android.Security.Keystore;
using EcosystemAi.Product;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using System.Text;
using System.Text.Json;

namespace EcosystemAi.Android;

// Device-local vault. Only ciphertext is stored in private preferences. The AES key
// is generated inside AndroidKeyStore, not shipped with the APK and not exported.
internal sealed class ModelPreferences(Context context)
{
    private const string Alias = "ecosystem-ai-model-preferences-v1";
    private readonly ISharedPreferences _prefs = context.GetSharedPreferences("ai-providers-v1", FileCreationMode.Private)!;
    internal sealed record Snapshot(SavedModelConfiguration Configuration, Dictionary<string, string> Credentials);
    private Java.Security.IKey Key()
    {
        using var store = KeyStore.GetInstance("AndroidKeyStore")!;
        store.Load(null);
        if (store.ContainsAlias(Alias)) return store.GetKey(Alias, null)!;
        using var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
        using var spec = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)!
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)!
            .SetRandomizedEncryptionRequired(true)!.Build();
        generator.Init(spec!);
        return generator.GenerateKey()!;
    }
    public Snapshot Read()
    {
        var value = _prefs.GetString("vault", null);
        if (value is null) return new(SavedModelConfiguration.Empty, []);
        try
        {
            var data = Convert.FromBase64String(value);
            if (data.Length < 29 || data.Length > 2 * 1024 * 1024 || data[0] != 1)
                throw new InvalidDataException();
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            using var spec = new GCMParameterSpec(128, data[1..13]);
            cipher.Init(CipherMode.DecryptMode, Key(), spec);
            cipher.UpdateAAD(Encoding.UTF8.GetBytes(Alias));
            var plaintext = cipher.DoFinal(data[13..])!;
            try
            {
                var snapshot = JsonSerializer.Deserialize<Snapshot>(plaintext) ?? throw new InvalidDataException();
                snapshot.Configuration.Validate();
                if (snapshot.Credentials is null || snapshot.Credentials.Count > 30
                    || snapshot.Credentials.Any(kv => !snapshot.Configuration.Providers.Any(p => p.Id == kv.Key)
                        || kv.Value.Length is < 8 or > 8192 || kv.Value.Any(char.IsControl))) throw new InvalidDataException();
                _ = snapshot.Configuration.Selected(id => snapshot.Credentials.GetValueOrDefault(id));
                return snapshot;
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext); }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        { throw new IOException("As configurações de IA não puderam ser abertas neste aparelho. Os dados foram preservados."); }
    }
    public void Reset()
    {
        if (!_prefs.Edit()!.Clear()!.Commit()) throw new IOException("Não foi possível redefinir as configurações de IA.");
    }
    public void Save(SavedModelConfiguration configuration, Dictionary<string, string> credentials)
    {
        configuration.Validate();
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(new Snapshot(configuration, credentials));
        try
        {
            using var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(CipherMode.EncryptMode, Key());
            cipher.UpdateAAD(Encoding.UTF8.GetBytes(Alias));
            var encrypted = cipher.DoFinal(plaintext)!;
            var iv = cipher.GetIV()!;
            if (iv.Length != 12) throw new IOException("Não foi possível proteger a configuração.");
            var data = new byte[1 + iv.Length + encrypted.Length];
            data[0] = 1; iv.CopyTo(data, 1); encrypted.CopyTo(data, 13);
            if (!_prefs.Edit()!.PutString("vault", Convert.ToBase64String(data))!.Commit())
                throw new IOException("Não foi possível salvar a configuração de IA.");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        { throw new IOException("Não foi possível proteger e salvar a configuração de IA. A configuração anterior foi preservada."); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext); }
    }
}
