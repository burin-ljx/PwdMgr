namespace PwdMgr.Core.Storage;

/// <summary>
/// “先写临时文件、落盘、再替换”的文件写入方式。替换时把上一版留成 <c>.bak</c>，
/// 保证任何一步失败都不会破坏原文件。
/// </summary>
internal static class AtomicFile
{
    private const int BufferSize = 64 * 1024;

    /// <summary>
    /// 直接写入已存在的文件（用于文件选择器预建的 0 字节占位文件）。
    /// 不走“临时文件 + 替换”，因为占位文件可能仍被选择器持有，删除或改名会失败。
    /// </summary>
    public static void WriteInPlace(string path, ReadOnlySpan<byte> content)
    {
        ClearReadOnly(path);

        using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSize,
            FileOptions.WriteThrough);

        stream.Write(content);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>异步版本，行为与 <see cref="WriteInPlace"/> 一致。</summary>
    public static async Task WriteInPlaceAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        ClearReadOnly(path);

        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>同步写入。</summary>
    public static void Write(string path, ReadOnlySpan<byte> content)
    {
        var tempPath = path + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       BufferSize,
                       FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            ReplaceWithBackup(tempPath, path);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>异步写入，行为与 <see cref="Write"/> 一致。</summary>
    public static async Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             BufferSize,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            ReplaceWithBackup(tempPath, path);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void ClearReadOnly(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    /// <summary>用临时文件替换目标文件，并把旧文件改名为 <c>.bak</c>。</summary>
    public static void ReplaceWithBackup(string tempPath, string destinationPath)
    {
        var backupPath = destinationPath + ".bak";
        if (!File.Exists(destinationPath))
        {
            File.Move(tempPath, destinationPath);
            return;
        }

        ClearReadOnly(destinationPath);

        try
        {
            File.Replace(tempPath, destinationPath, backupPath, ignoreMetadataErrors: true);
        }
        catch (IOException)
        {
            // 某些文件系统不支持替换语义，退化为“复制备份 + 覆盖移动”。
            File.Copy(destinationPath, backupPath, overwrite: true);
            File.Move(tempPath, destinationPath, overwrite: true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 临时文件清理失败不影响主流程，下次写入会覆盖它。
        }
    }
}
