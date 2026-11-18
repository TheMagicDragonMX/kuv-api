namespace kuv_api.Models.Files;

/// <summary>
/// A file read from an <see cref="Services.IFileStore"/>. Whoever receives it owns <paramref name="Content"/> and must
/// dispose it.
/// </summary>
/// <param name="Content">
/// The bytes of the file. The stream can be read from the start and, whatever the store, it can seek, so a video can
/// be served in ranges.
/// </param>
/// <param name="Length">
/// Size of the file, in bytes.
/// </param>
/// <param name="LastModified">
/// The moment the file was last written.
/// </param>
public sealed record StoredFile(Stream Content, long Length, DateTimeOffset LastModified);
