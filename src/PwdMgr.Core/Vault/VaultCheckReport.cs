namespace PwdMgr.Core.Vault;

/// <summary>密码库自检结果。</summary>
/// <param name="ActiveCount">有效条目数。</param>
/// <param name="DeletedCount">回收站条目数。</param>
/// <param name="FileSize">密码库文件大小（字节）。</param>
public sealed record VaultCheckReport(int ActiveCount, int DeletedCount, long FileSize);
