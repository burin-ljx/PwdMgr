# PwdMgr

本地离线密码管理器（Windows / C# / WinUI 3）。数据只存在两个文件里：加密的密码库 + 你保管的钥匙文件。软件运行时不联任何网。

> 当前进度：**M1 完成**（Core 加密与存储）、**M2 完成**（WinUI 3 界面骨架：欢迎 / 创建 / 解锁 / 主界面）。
> 条目详情已可直接编辑并保存；新增/删除条目、密码生成器、剪贴板复制属于后续阶段。

## 界面

| 欢迎页 | 创建密码库 |
|---|---|
| ![欢迎页](docs/ui/welcome.png) | ![创建密码库](docs/ui/create-vault.png) |

| 打开密码库 | 主界面 |
|---|---|
| ![打开密码库](docs/ui/unlock.png) | ![主界面](docs/ui/vault.png) |

条目字段直接可改，出现改动后卡片底部出现“保存修改 / 放弃修改”：

![编辑条目](docs/ui/vault-editing.png)

界面使用 WinUI 3 + Fluent 主题资源 + Mica 背景，跟随系统深色/浅色设置。

## 目录结构

```
PwdMgr.sln
build.ps1                      用 Visual Studio 的 MSBuild 构建整个解决方案
src/PwdMgr.Core/               加解密、文件格式、存取、数据模型（无界面、可单测）
src/PwdMgr.App/                WinUI 3 界面：页面、ViewModel、文件选择、窗口
tests/PwdMgr.Core.Tests/       xUnit 测试
tools/PwdMgr.Cli/              开发期命令行工具，用于无界面验证存储
docs/技术方案-v1.md            技术方案与设计决策
docs/ui/                       界面截图
```

## 环境要求

- Windows 10 1809+ / Windows 11
- .NET SDK 8.0 或更高（项目目标框架 `net8.0` / `net8.0-windows10.0.19041.0`）
- **Visual Studio 2022 / 2026**（安装「Windows 应用开发」或「.NET 桌面开发」工作负载）
  - WinUI 3 工程依赖 VS 自带的 Appx MSBuild 任务，`dotnet build` 无法编译 `PwdMgr.App`。

## 构建

```powershell
.\build.ps1                      # Debug
.\build.ps1 -Configuration Release
```

脚本会用 `vswhere` 找到 Visual Studio 的 MSBuild 再编译整个解决方案。

只构建不依赖界面的部分（Core / CLI / 测试）时，仍然可以用 dotnet CLI：

```powershell
dotnet build tests/PwdMgr.Core.Tests/PwdMgr.Core.Tests.csproj -m:1
```

## 运行

```powershell
.\src\PwdMgr.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\PwdMgr.App.exe
```

也支持带参数直接打开一个密码库（便于验收与调试）：

```powershell
.\PwdMgr.App.exe .\demo\vault.pwdb .\demo\vault.key
```

## 测试

```powershell
dotnet test tests/PwdMgr.Core.Tests/PwdMgr.Core.Tests.csproj
```

如果所在环境的测试宿主无法启动（例如受限沙箱禁止命名管道），直接运行测试程序：

```powershell
dotnet tests\PwdMgr.Core.Tests\bin\Debug\net8.0\PwdMgr.Core.Tests.dll
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
- 界面不提供「打开网址」按钮，避免把浏览器联网行为带进流程。

## 已知限制

- 条目**可以查看和编辑**：改动后底部出现保存条；切换条目、搜索、上锁、关窗前会自动保存，保存失败时会拦下操作并提示。
- 尚未实现：新增条目、删除条目、回收站界面、密码生成器、剪贴板复制与自动清空、自动锁定。
- 程序目录下的 `startup.log` 记录启动与异常信息（Debug 构建记录每个启动步骤，Release 只记录异常），可随时删除。
