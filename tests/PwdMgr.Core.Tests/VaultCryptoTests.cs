using PwdMgr.Core.Crypto;
using Xunit;

namespace PwdMgr.Core.Tests;

public sealed class VaultCryptoTests
{
    private static readonly byte[] Aad = "header-bytes"u8.ToArray();

    [Fact]
    public void EncryptThenDecrypt_RoundTrips()
    {
        var dataKey = VaultCrypto.CreateDataKey();
        var nonce = VaultCrypto.CreateNonce([]);
        var plaintext = "机密内容"u8.ToArray();

        var (ciphertext, tag) = VaultCrypto.Encrypt(plaintext, dataKey, nonce, Aad);

        Assert.True(VaultCrypto.TryDecrypt(ciphertext, tag, dataKey, nonce, Aad, out var decrypted));
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TryDecrypt_WithDifferentKey_ReturnsFalse()
    {
        var dataKey = VaultCrypto.CreateDataKey();
        var otherKey = VaultCrypto.CreateDataKey();
        var nonce = VaultCrypto.CreateNonce([]);
        var (ciphertext, tag) = VaultCrypto.Encrypt("data"u8, dataKey, nonce, Aad);

        Assert.False(VaultCrypto.TryDecrypt(ciphertext, tag, otherKey, nonce, Aad, out var plaintext));
        Assert.Empty(plaintext);
    }

    [Fact]
    public void TryDecrypt_WithTamperedTag_ReturnsFalse()
    {
        var dataKey = VaultCrypto.CreateDataKey();
        var nonce = VaultCrypto.CreateNonce([]);
        var (ciphertext, tag) = VaultCrypto.Encrypt("data"u8, dataKey, nonce, Aad);

        tag[0] ^= 0xFF;

        Assert.False(VaultCrypto.TryDecrypt(ciphertext, tag, dataKey, nonce, Aad, out _));
    }

    [Fact]
    public void TryDecrypt_WithTamperedAad_ReturnsFalse()
    {
        var dataKey = VaultCrypto.CreateDataKey();
        var nonce = VaultCrypto.CreateNonce([]);
        var (ciphertext, tag) = VaultCrypto.Encrypt("data"u8, dataKey, nonce, Aad);
        var otherAad = "header-bytez"u8.ToArray();

        Assert.False(VaultCrypto.TryDecrypt(ciphertext, tag, dataKey, nonce, otherAad, out _));
    }

    [Fact]
    public void Encrypt_WithSamePlaintextAndDifferentNonce_ProducesDifferentCiphertext()
    {
        var dataKey = VaultCrypto.CreateDataKey();
        var plaintext = "相同内容"u8.ToArray();

        var (first, _) = VaultCrypto.Encrypt(plaintext, dataKey, VaultCrypto.CreateNonce([]), Aad);
        var (second, _) = VaultCrypto.Encrypt(plaintext, dataKey, VaultCrypto.CreateNonce([]), Aad);

        Assert.False(first.AsSpan().SequenceEqual(second.AsSpan()));
    }

    [Fact]
    public void CreateNonce_DoesNotReturnPreviousNonce()
    {
        var previous = VaultCrypto.CreateNonce([]);

        for (var i = 0; i < 100; i++)
        {
            var next = VaultCrypto.CreateNonce(previous);
            Assert.False(next.AsSpan().SequenceEqual(previous));
        }
    }
}
