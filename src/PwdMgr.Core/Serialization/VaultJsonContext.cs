using System.Text.Json.Serialization;
using PwdMgr.Core.Crypto;
using PwdMgr.Core.Models;
using PwdMgr.Core.Vault;

namespace PwdMgr.Core.Serialization;

/// <summary>
/// System.Text.Json 源生成上下文。使用源生成而非反射，既能被裁剪，也避免运行时动态代码。
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(VaultPayload))]
[JsonSerializable(typeof(VaultHeader))]
[JsonSerializable(typeof(KeyFileModel))]
internal sealed partial class VaultJsonContext : JsonSerializerContext
{
}
