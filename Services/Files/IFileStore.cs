using kuv_api.Models.Files;

namespace kuv_api.Services;

/// <summary>
/// Where the files of the API (programs, videos, profile pictures) are kept. A file is named by a key, a relative path
/// with forward slashes such as <c>creations/7/program.json</c>. There are two stores: <see cref="DiskFileStore"/>
/// keeps them in a folder of this machine (what the server uses) and <see cref="RemoteFileStore"/> keeps them on the
/// server, so an API running anywhere else reads and writes the same files.
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Writes a file, replacing the one with the same key. Folders are created when needed, and a file being replaced
    /// is never seen half written.
    /// </summary>
    /// <param name="key">
    /// Where the file goes.
    /// </param>
    /// <param name="content">
    /// What to write, from its current position.
    /// </param>
    Task Write(string key, Stream content);

    /// <summary>
    /// Opens a file for reading.
    /// </summary>
    /// <param name="key">
    /// The file wanted.
    /// </param>
    /// <returns>
    /// The file, or null when there is none with that key (an expected state: a user without a picture).
    /// </returns>
    Task<StoredFile?> Open(string key);

    /// <summary>
    /// When a file was last written, without opening it.
    /// </summary>
    /// <param name="key">
    /// The file asked about.
    /// </param>
    /// <returns>
    /// The moment, or null when there is no such file.
    /// </returns>
    Task<DateTimeOffset?> LastModified(string key);

    /// <summary>
    /// Deletes a file. Deleting one that does not exist changes nothing.
    /// </summary>
    /// <param name="key">
    /// The file to delete.
    /// </param>
    Task Delete(string key);

    /// <summary>
    /// Deletes a folder with everything in it. Deleting one that does not exist changes nothing.
    /// </summary>
    /// <param name="key">
    /// The folder to delete, such as <c>creations/7</c>.
    /// </param>
    Task DeleteFolder(string key);
}
