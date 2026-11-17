using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RevendaVeiculos.Application.Interfaces;

namespace RevendaVeiculos.Infrastructure.Security;

public class AesGcmCryptoService : ICryptoService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string PrefixCifrado = "ENC:v1:";

    private readonly byte[] _encryptionKey;
    private readonly byte[] _hmacKey;

    public AesGcmCryptoService(IConfiguration configuration)
    {
        var masterSecret = configuration["Security:MasterEncryptionKey"]
            ?? "FIAP-SOAT-FASE5-AES256-GCM-MASTER-KEY-2026-KMS-PROTECTED";

        var hmacSecret = configuration["Security:HmacDeterministicKey"]
            ?? "FIAP-SOAT-FASE5-HMAC-SHA256-INDEX-KEY-2026-KMS-PROTECTED";

        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(masterSecret));
        _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(hmacSecret));
    }

    public string Criptografar(string textoClaro)
    {
        if (string.IsNullOrEmpty(textoClaro))
            return string.Empty;

        if (textoClaro.StartsWith(PrefixCifrado, StringComparison.Ordinal))
            return textoClaro;

        var plainBytes = Encoding.UTF8.GetBytes(textoClaro);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_encryptionKey, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var payload = new byte[NonceSize + TagSize + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(cipherBytes, 0, payload, NonceSize + TagSize, cipherBytes.Length);

        return PrefixCifrado + Convert.ToBase64String(payload);
    }

    public string Descriptografar(string textoCifrado)
    {
        if (string.IsNullOrEmpty(textoCifrado))
            return string.Empty;

        if (!textoCifrado.StartsWith(PrefixCifrado, StringComparison.Ordinal))
            return textoCifrado;

        var base64 = textoCifrado[PrefixCifrado.Length..];
        var payload = Convert.FromBase64String(base64);

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherBytes = new byte[payload.Length - NonceSize - TagSize];
        var plainBytes = new byte[cipherBytes.Length];

        Buffer.BlockCopy(payload, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(payload, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(payload, NonceSize + TagSize, cipherBytes, 0, cipherBytes.Length);

        using var aesGcm = new AesGcm(_encryptionKey, TagSize);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    public string GerarHashDeterministico(string valorNormalizado)
    {
        if (string.IsNullOrWhiteSpace(valorNormalizado))
            return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(valorNormalizado.Trim().ToLowerInvariant());
        using var hmac = new HMACSHA256(_hmacKey);
        var hashBytes = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
