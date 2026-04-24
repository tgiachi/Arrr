using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Arrr.Core.Attributes;

namespace Arrr.Core.Utils;

/// <summary>
/// AES-256-GCM encryption tied to the local machine identity.
/// Encrypted values are prefixed with "enc:" to allow transparent migration.
/// </summary>
public static class EncryptionUtils
{
    private const string Prefix = "enc:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly Lazy<byte[]> _key = new(DeriveKey);

    public static string Encrypt(string plaintext)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[data.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key.Value, TagSize);
        aes.Encrypt(nonce, data, ciphertext, tag);

        var blob = new byte[NonceSize + ciphertext.Length + TagSize];
        nonce.CopyTo(blob, 0);
        ciphertext.CopyTo(blob, NonceSize);
        tag.CopyTo(blob, NonceSize + ciphertext.Length);

        return Prefix + Convert.ToBase64String(blob);
    }

    public static string Decrypt(string value)
    {
        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
            return value;

        var blob = Convert.FromBase64String(value[Prefix.Length..]);
        var nonce = blob[..NonceSize];
        var tag = blob[^TagSize..];
        var ciphertext = blob[NonceSize..^TagSize];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key.Value, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public static bool IsEncrypted(string value) =>
        value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// Walks all public string properties decorated with <c>[Sensitive]</c> and
    /// either encrypts or decrypts them in-place.
    /// </summary>
    public static void ApplySensitiveFields(object config, bool decrypt)
    {
        var props = config.GetType()
                          .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                          .Where(p => p.PropertyType == typeof(string)
                                   && p.CanRead && p.CanWrite
                                   && p.GetCustomAttribute<SensitiveAttribute>() is not null);

        foreach (var prop in props)
        {
            var value = prop.GetValue(config) as string;
            if (string.IsNullOrEmpty(value)) continue;

            prop.SetValue(config, decrypt
                ? Decrypt(value)
                : IsEncrypted(value) ? value : Encrypt(value));
        }
    }

    private static byte[] DeriveKey()
    {
        var machineId = File.Exists("/etc/machine-id")
            ? File.ReadAllText("/etc/machine-id").Trim()
            : Environment.MachineName;

        var salt = "arrr-plugin-config-v1"u8.ToArray();
        return Rfc2898DeriveBytes.Pbkdf2(machineId, salt, 100_000, HashAlgorithmName.SHA256, 32);
    }
}
