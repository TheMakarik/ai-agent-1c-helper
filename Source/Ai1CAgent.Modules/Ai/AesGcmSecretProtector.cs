using System.Security.Cryptography;
using System.Text;
using Ai1CAgent.Modules.Ai.Interfaces;
using Ai1CAgent.Modules.Storage;
using Microsoft.Extensions.Logging;

namespace Ai1CAgent.Modules.Ai;

/// <summary>
/// Шифрует секреты симметричным ключом AES-256-GCM, мастер-ключ хранится рядом с настройками
/// в каталоге пользователя (AppData). На Unix-системах файл ключа доступен только владельцу.
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly ILogger<AesGcmSecretProtector> _logger;
    private readonly string _keyFile;
    private readonly Lazy<byte[]> _key;

    public AesGcmSecretProtector(ILogger<AesGcmSecretProtector> logger, string? keyFilePath = null)
    {
        _logger = logger;
        _keyFile = keyFilePath ?? AppDataPaths.SecretKeyFile;
        _key = new Lazy<byte[]>(LoadOrCreateKey, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key.Value, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        cipher.CopyTo(payload, NonceSize + TagSize);

        return Convert.ToBase64String(payload);
    }

    public bool TryUnprotect(string protectedValue, out string plaintext)
    {
        plaintext = string.Empty;

        if (string.IsNullOrEmpty(protectedValue))
            return true;

        try
        {
            var payload = Convert.FromBase64String(protectedValue);

            if (payload.Length < NonceSize + TagSize)
                return false;

            var nonce = payload.AsSpan(0, NonceSize);
            var tag = payload.AsSpan(NonceSize, TagSize);
            var cipher = payload.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key.Value, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);

            plaintext = Encoding.UTF8.GetString(plain);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private byte[] LoadOrCreateKey()
    {
        var path = _keyFile;

        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);

            if (existing.Length == KeySize)
                return existing;

            _logger.LogWarning(
                "Файл ключа шифрования повреждён, он будет пересоздан: {Path}. " +
                "Ранее сохранённые API-ключи станут нечитаемыми",
                path);
        }

        var key = RandomNumberGenerator.GetBytes(KeySize);
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
            System.IO.Directory.CreateDirectory(directory);

        File.WriteAllBytes(path, key);
        RestrictPermissions(path);

        return key;
    }

    private void RestrictPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException exception)
        {
            _logger.LogDebug(exception, "Платформа не поддерживает ограничение прав на файл ключа: {Path}", path);
        }
    }
}
