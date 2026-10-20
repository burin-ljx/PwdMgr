using PwdMgr.Core.Vault;

// 开发期用的命令行外壳：不参与正式发布，只用来在没有界面的阶段验证加密存储是否可用。

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

try
{
    switch (args[0])
    {
        case "create":
            return await CreateAsync(args);
        case "list":
            return await ListAsync(args);
        case "add":
            return await AddAsync(args);
        default:
            Console.Error.WriteLine($"未知命令：{args[0]}");
            PrintUsage();
            return 1;
    }
}
catch (VaultException ex)
{
    Console.Error.WriteLine($"失败[{ex.Code}]：{ex.Message}");
    return 2;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine($"失败：{ex.Message}");
    return 3;
}

static void PrintUsage() => Console.WriteLine(
    """
    用法：
      create <密码库路径> <钥匙路径>                                    创建一个新的空密码库
      list   <密码库路径> <钥匙路径>                                    列出所有条目
      add    <密码库路径> <钥匙路径> --title <标题> [--username <用户名>] [--password <密码>] [--url <网址>] [--notes <备注>]

    示例：
      add .\vault.pwdb .\vault.key --title GitHub --username alice --password "s3cret!"
    """);

static async Task<int> CreateAsync(string[] args)
{
    if (args.Length < 3)
    {
        return MissingArguments();
    }

    await using var session = await VaultSession.CreateAsync(args[1], args[2]);
    await session.SaveAsync();

    Console.WriteLine($"已创建密码库：{session.VaultPath}");
    Console.WriteLine($"已生成钥匙文件：{session.KeyFilePath}");
    Console.WriteLine($"钥匙指纹：{session.KeyId}");
    Console.WriteLine("请把钥匙文件复制一份到别处保管——丢失后密码库将无法打开。");
    return 0;
}

static async Task<int> ListAsync(string[] args)
{
    if (args.Length < 3)
    {
        return MissingArguments();
    }

    await using var session = await VaultSession.OpenAsync(args[1], args[2]);
    var items = session.Document.GetActiveItems();

    Console.WriteLine($"密码库：{session.VaultPath}");
    Console.WriteLine($"钥匙指纹：{session.KeyId}");
    Console.WriteLine($"条目数：{items.Count}");

    foreach (var item in items)
    {
        Console.WriteLine($"  - {item.Title}  {item.Username}");
    }

    return 0;
}

static async Task<int> AddAsync(string[] args)
{
    if (args.Length < 3)
    {
        return MissingArguments();
    }

    var title = ReadOption(args, "--title");
    if (string.IsNullOrWhiteSpace(title))
    {
        Console.Error.WriteLine("缺少 --title 参数。");
        return 1;
    }

    await using var session = await VaultSession.OpenAsync(args[1], args[2]);
    var item = session.Document.Add(
        title,
        ReadOption(args, "--username") ?? string.Empty,
        ReadOption(args, "--password") ?? string.Empty,
        ReadOption(args, "--url") ?? string.Empty,
        ReadOption(args, "--notes") ?? string.Empty);

    await session.SaveAsync();
    Console.WriteLine($"已添加：{item.Title}（{item.Id}）");
    return 0;
}

static int MissingArguments()
{
    Console.Error.WriteLine("参数不足。");
    PrintUsage();
    return 1;
}

static string? ReadOption(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
