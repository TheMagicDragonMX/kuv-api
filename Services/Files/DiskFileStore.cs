using kuv_api.Models.Files;

namespace kuv_api.Services;

/// <summary>
/// Keeps the files in a folder of this machine. It is what the server uses, and what a local API uses when it has to
/// work without a connection (<c>Files:Mode</c> = <c>Disk</c>).
/// </summary>
/// <param name="root">
/// The folder that holds every file. It is created when the first file is written.
/// </param>
public sealed class DiskFileStore(string root) : IFileStore
{
    /// <inheritdoc />
    public async Task Write(string key, Stream content)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written aside and then moved over, so a failure never leaves a half-written file
        var temporary = path + ".tmp";

        await using (var output = File.Create(temporary))
        {
            await content.CopyToAsync(output);
        }

        File.Move(temporary, path, overwrite: true);
    }

    /// <inheritdoc />
    public Task<StoredFile?> Open(string key)
    {
        var path = PathOf(key);

        if (!File.Exists(path))
        {
            return Task.FromResult<StoredFile?>(null);
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var file = new StoredFile(stream, stream.Length, File.GetLastWriteTimeUtc(path));

        return Task.FromResult<StoredFile?>(file);
    }

    /// <inheritdoc />
    public Task<DateTimeOffset?> LastModified(string key)
    {
        var path = PathOf(key);
        DateTimeOffset? moment = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;

        return Task.FromResult(moment);
    }

    /// <inheritdoc />
    public Task Delete(string key)
    {
        File.Delete(PathOf(key));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteFolder(string key)
    {
        var path = PathOf(key);

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Where the file of a key is on disk.
    /// </summary>
    private string PathOf(string key)
    {
        return Path.Combine(root, FileKeys.Check(key).Replace('/', Path.DirectorySeparatorChar));
    }
}
