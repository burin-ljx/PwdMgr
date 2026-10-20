using System.Text.Json.Nodes;
using PwdMgr.Core.Crypto;
using PwdMgr.Core.Vault;
using Xunit;

namespace PwdMgr.Core.Tests;

public sealed class KeyFileTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsKeyIdAndKeyMaterial()
    {
        using var temp = new TestVault();
        using var keyPair = VaultKeyPair.Create();

        KeyFileStore.Save(temp.KeyPath, keyPair);
        using var loaded = KeyFileStore.Load(temp.KeyPath);

        Assert.Equal(keyPair.KeyId, loaded.KeyId);

        var dataKey = VaultCrypto.CreateDataKey();
        var wrapped = keyPair.WrapDataKey(dataKey);

        Assert.Equal(dataKey, loaded.UnwrapDataKey(wrapped));
    }

    [Fact]
    public void Save_WritesExpectedJsonFields()
    {
        using var temp = new TestVault();
        using var keyPair = VaultKeyPair.Create();

        KeyFileStore.Save(temp.KeyPath, keyPair);

        var node = JsonNode.Parse(File.ReadAllText(temp.KeyPath))!.AsObject();

        Assert.Equal(1, node["format"]!.GetValue<int>());
        Assert.Equal("RSAPrivateKey", node["type"]!.GetValue<string>());
        Assert.Equal(keyPair.KeyId, node["keyId"]!.GetValue<string>());
        Assert.Equal("RSA-3072", node["algorithm"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(node["privateKeyPkcs8"]!.GetValue<string>()));
    }

    [Fact]
    public void Load_MissingFile_ReportsKeyFileNotFound()
    {
        using var temp = new TestVault();
        var missingPath = Path.Combine(temp.Root, "missing.key");

        var ex = Assert.Throws<VaultException>(() => KeyFileStore.Load(missingPath));

        Assert.Equal(VaultErrorCode.KeyFileNotFound, ex.Code);
    }

    [Fact]
    public void Load_GarbageContent_ReportsInvalidKeyFile()
    {
        using var temp = new TestVault();
        File.WriteAllText(temp.KeyPath, "这不是钥匙文件");

        var ex = Assert.Throws<VaultException>(() => KeyFileStore.Load(temp.KeyPath));

        Assert.Equal(VaultErrorCode.InvalidKeyFile, ex.Code);
    }

    [Fact]
    public void Load_TamperedKeyId_ReportsInvalidKeyFile()
    {
        using var temp = new TestVault();
        using var keyPair = VaultKeyPair.Create();
        KeyFileStore.Save(temp.KeyPath, keyPair);

        var node = JsonNode.Parse(File.ReadAllText(temp.KeyPath))!.AsObject();
        node["keyId"] = "0000000000000000";
        File.WriteAllText(temp.KeyPath, node.ToJsonString());

        var ex = Assert.Throws<VaultException>(() => KeyFileStore.Load(temp.KeyPath));

        Assert.Equal(VaultErrorCode.InvalidKeyFile, ex.Code);
    }

    [Fact]
    public void Load_InvalidPrivateKey_ReportsInvalidKeyFile()
    {
        using var temp = new TestVault();
        using var keyPair = VaultKeyPair.Create();
        KeyFileStore.Save(temp.KeyPath, keyPair);

        var node = JsonNode.Parse(File.ReadAllText(temp.KeyPath))!.AsObject();
        node["privateKeyPkcs8"] = "bm90LWEta2V5";
        File.WriteAllText(temp.KeyPath, node.ToJsonString());

        var ex = Assert.Throws<VaultException>(() => KeyFileStore.Load(temp.KeyPath));

        Assert.Equal(VaultErrorCode.InvalidKeyFile, ex.Code);
    }
}
