using kuv_api.Models.Exceptions;

namespace kuv_api.Services;

/// <summary>
/// Keeps the profile pictures. Each one is a file in the <see cref="IFileStore"/>, in <c>pfp</c>, named after the user's id and the type of
/// the image (<c>7.png</c>, <c>7.jpg</c>, <c>7.webp</c> or <c>7.gif</c>); a user has at most one. Nothing about the
/// pictures is in the database: a user has a picture when its file exists.
/// </summary>
public sealed class ProfilePhotoService(IFileStore files)
{
    /// <summary>
    /// Biggest picture accepted, in bytes.
    /// </summary>
    public const int MaxPhotoBytes = 300 * 1024;

    /// <summary>
    /// The types of image accepted: the extension the file gets and the content type it is served with.
    /// </summary>
    private static readonly (string Extension, string ContentType)[] Types =
    [
        ("png", "image/png"),
        ("jpg", "image/jpeg"),
        ("webp", "image/webp"),
        ("gif", "image/gif")
    ];

    /// <summary>
    /// What every error about a picture says.
    /// </summary>
    private const string InvalidPhotoMessage = "La foto debe ser una imagen PNG, JPEG, WebP o GIF de hasta 300 KB.";

    /// <summary>
    /// Saves a picture as the profile picture of a user, replacing the one they had.
    /// </summary>
    /// <param name="userId">
    /// The user the picture is for.
    /// </param>
    /// <param name="file">
    /// The uploaded image.
    /// </param>
    /// <exception cref="BadRequestException">
    /// The file is empty, too big, or not a PNG, JPEG, WebP or GIF. The type is read from the content of the file,
    /// not from its name or the content type the client says.
    /// </exception>
    public async Task Save(int userId, IFormFile file)
    {
        if (file.Length == 0 || file.Length > MaxPhotoBytes)
        {
            throw new BadRequestException(InvalidPhotoMessage);
        }

        var bytes = new byte[file.Length];

        await using (var stream = file.OpenReadStream())
        {
            await stream.ReadExactlyAsync(bytes);
        }

        var extension = DetectExtension(bytes);

        using var content = new MemoryStream(bytes);
        await files.Write(KeyFor(userId, extension), content);

        await DeleteExcept(userId, extension);
    }

    /// <summary>
    /// Deletes the profile picture of a user. Doing it when they have none changes nothing.
    /// </summary>
    /// <param name="userId">
    /// The user whose picture is removed.
    /// </param>
    public Task Delete(int userId)
    {
        return DeleteExcept(userId, keepExtension: string.Empty);
    }

    /// <summary>
    /// The profile picture of a user.
    /// </summary>
    /// <param name="userId">
    /// The user whose picture is wanted.
    /// </param>
    /// <returns>
    /// The file, with its type and the moment it was saved.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// The user has no picture.
    /// </exception>
    public async Task<StoredPhoto> Find(int userId)
    {
        foreach (var (extension, contentType) in Types)
        {
            var file = await files.Open(KeyFor(userId, extension));

            if (file is not null)
            {
                return new StoredPhoto(file.Content, contentType, file.LastModified);
            }
        }

        throw new NotFoundException("Este usuario no tiene foto.");
    }

    /// <summary>
    /// Where a user's picture can be fetched from, or null when they have none (a legitimate state, not an error).
    /// The address ends in the moment the picture was saved (<c>?v=</c>), so it changes whenever the picture does and
    /// browsers can keep it for as long as they like.
    /// </summary>
    /// <param name="userId">
    /// The user whose picture is wanted.
    /// </param>
    /// <returns>
    /// A path on this API such as <c>/users/7/photo?v=639000000000000000</c>, or null.
    /// </returns>
    public async Task<string?> UrlFor(int userId)
    {
        foreach (var (extension, _) in Types)
        {
            var savedAt = await files.LastModified(KeyFor(userId, extension));

            if (savedAt is not null)
            {
                return $"/users/{userId}/photo?v={savedAt.Value.UtcTicks}";
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes the files of a user's picture of every type but one.
    /// </summary>
    private async Task DeleteExcept(int userId, string keepExtension)
    {
        foreach (var (extension, _) in Types)
        {
            if (extension != keepExtension)
            {
                await files.Delete(KeyFor(userId, extension));
            }
        }
    }

    /// <summary>
    /// The key, in the file store, of the file of a user's picture of a type.
    /// </summary>
    private static string KeyFor(int userId, string extension)
    {
        return $"pfp/{userId}.{extension}";
    }

    /// <summary>
    /// The type of an image, read from its first bytes.
    /// </summary>
    /// <returns>
    /// The extension of the type.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The bytes are not a PNG, JPEG, WebP or GIF.
    /// </exception>
    private static string DetectExtension(byte[] bytes)
    {
        var start = bytes.AsSpan();

        if (start.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "png";
        }

        if (start.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return "jpg";
        }

        if (start.StartsWith("GIF87a"u8) || start.StartsWith("GIF89a"u8))
        {
            return "gif";
        }

        // WebP is a RIFF file: "RIFF", the size, then "WEBP"
        if (start.Length >= 12 && start[..4].SequenceEqual("RIFF"u8) && start.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        throw new BadRequestException(InvalidPhotoMessage);
    }
}

/// <summary>
/// The file of a profile picture. Whoever receives it must dispose its stream.
/// </summary>
/// <param name="Content">
/// The bytes of the image.
/// </param>
/// <param name="ContentType">
/// The content type to serve it with.
/// </param>
/// <param name="LastModified">
/// The moment the picture was saved.
/// </param>
public sealed record StoredPhoto(Stream Content, string ContentType, DateTimeOffset LastModified);
