namespace PwdMgr.Core.Storage;

/// <summary>
/// “先写临时文件、落盘、再替换”的文件写入方式。替换时把上一版留成 <c>.bak</c>，
/// 保证任何一步失败都不会破坏原文件。
/// </summary>
internal static class AtomicFile
{
    private const int BufferSize = 64 * 1024;

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

    /// <summary>用临时文件替换目标文件，并把旧文件改名为 <c>.bak</c>。</summary>
    public static void ReplaceWithBackup(string tempPath, string destinationPath)
    {
        var backupPath = destinationPath + ".bak";
        if (!File.Exists(destinationPath))
        {
            File.Move(tempPath, destinationPath);
            return;
        }

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
