# PwdMgr

本地离线密码管理器（Windows / C# / WinUI 3）。数据只存在两个文件里：加密的密码库 + 你保管的钥匙文件。软件运行时不联任何网。

> 当前进度：**M1 已完成**（Core 加密存储 + 命令行验证 + 测试）。界面（M2 起）尚未开始。

## 目录结构

```
PwdMgr.sln
src/PwdMgr.Core/        加解密、文件格式、存取、数据模型（无界面、可单测）
tests/PwdMgr.Core.Tests/  xUnit 测试
tools/PwdMgr.Cli/       开发期命令行工具，用来在没有界面的阶段验证存储
docs/技术方案-v1.md     技术方案与设计决策
```

## 环境要求

- Windows 10 1809+ / Windows 11
- .NET SDK 8.0 或更高（项目目标框架为 `net8.0`）

## 构建

```powershell
dotnet build PwdMgr.sln -m:1
```

`-m:1` 是给「MSBuild 多节点通信被环境限制」的机器用的；在普通机器上去掉也能构建。

## 测试

```powershell
dotnet test tests/PwdMgr.Core.Tests/PwdMgr.Core.Tests.csproj
```

如果所在环境的测试宿主无法启动（例如受限沙箱禁止命名管道），可以绕过 `dotnet test`，直接运行测试程序：

```powershell
dotnet tests/PwdMgr.Core.Tests/bin/Debug/net8.0/PwdMgr.Core.Tests.dll
```

测试覆盖：加密往返、错钥匙、密文/头部篡改、截断、版本不符、缺文件、并发占用、备份与恢复、明文不落盘、条目增删改查与搜索、回收站、密钥文件校验。

## 命令行工具（仅开发期使用）

```powershell
# 创建密码库与钥匙（两个文件请分开保管）
dotnet run --project tools/PwdMgr.Cli -- create .\demo\vault.pwdb .\demo\vault.key

# 添加一条
dotnet run --project tools/PwdMgr.Cli -- add .\demo\vault.pwdb .\demo\vault.key --title GitHub --username alice --password "s3cret!"

# 列出条目
dotnet run --project tools/PwdMgr.Cli -- list .\demo\vault.pwdb .\demo\vault.key
```

## 安全模型（摘要）

- 密码库：AES-256-GCM 整体加密，密文前有明文头部（算法标识、钥匙指纹、被包裹的数据密钥）。
- 钥匙文件：RSA-3072 私钥（PKCS#8，Base64）。**私钥不加密**，也就意味着「钥匙文件 + 密码库」同时泄露即等于数据泄露。
- 每次保存换随机 nonce，先写临时文件、落盘、再替换，并保留 `.bak`；写入后立即读回校验。
- 钥匙文件丢失后没有任何找回机制。请把它复制到与密码库不同的位置保管。
