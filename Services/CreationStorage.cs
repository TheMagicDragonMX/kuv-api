using System.Text;
using System.Text.Json.Nodes;
using kuv_api.Models.Files;

namespace kuv_api.Services;

/// <summary>
/// Keeps the files of the creations in the <see cref="IFileStore"/>. Each creation has its own folder, <c>creations/{id}</c>, holding its
/// program file (<c>program.json</c>) and its video (<c>preview.webm</c> or <c>preview.mp4</c>). The database only
/// holds what describes the creation, never its program.
/// </summary>
public sealed class CreationStorage(IFileStore files)
{
    /// <summary>
    /// Name of the program file inside the folder of a creation.
    /// </summary>
    public const string ProgramFileName = "program.json";

    /// <summary>
    /// Name, without extension, of the video inside the folder of a creation.
    /// </summary>
    private const string VideoBaseName = "preview";

    /// <summary>
    /// The extensions a video can have. Anything else is stored as <c>.webm</c>, so the name of the uploaded file
    /// never decides what is written to disk.
    /// </summary>
    private static readonly string[] VideoExtensions = [".webm", ".mp4"];

    /// <summary>
    /// The folder of a creation, as a key of the file store.
    /// </summary>
    private static string FolderOf(int creationId) => $"creations/{creationId}";

    /// <summary>
    /// Writes the program file of a creation, replacing the one it had.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <param name="programFile">
    /// The validated program file.
    /// </param>
    public async Task WriteProgramFile(int creationId, JsonNode programFile)
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(programFile.ToJsonString()));

        await files.Write($"{FolderOf(creationId)}/{ProgramFileName}", content);
    }

    /// <summary>
    /// Reads the program file of a creation.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <returns>
    /// The program file, or null when the creation has none stored.
    /// </returns>
    public async Task<JsonNode?> ReadProgramFile(int creationId)
    {
        var file = await files.Open($"{FolderOf(creationId)}/{ProgramFileName}");

        if (file is null)
        {
            return null;
        }

        // Copied to memory first: the parser reads without waiting, which a download must never do
        await using var content = file.Content;
        using var buffer = new MemoryStream();

        await content.CopyToAsync(buffer);
        buffer.Position = 0;

        return JsonNode.Parse(buffer);
    }

    /// <summary>
    /// Saves an uploaded video as the video of a creation, replacing the one it had.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <param name="file">
    /// The uploaded video.
    /// </param>
    public async Task WriteVideo(int creationId, IFormFile file)
    {
        await DeleteVideos(creationId);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = VideoBaseName + (VideoExtensions.Contains(extension) ? extension : ".webm");

        await using var content = file.OpenReadStream();
        await files.Write($"{FolderOf(creationId)}/{fileName}", content);
    }

    /// <summary>
    /// Opens the video of a creation, whichever of the allowed extensions it was saved with.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <returns>
    /// The video with its content type, or null when it is not stored.
    /// </returns>
    public async Task<(StoredFile Video, string ContentType)?> OpenVideo(int creationId)
    {
        foreach (var extension in VideoExtensions)
        {
            var video = await files.Open($"{FolderOf(creationId)}/{VideoBaseName}{extension}");

            if (video is not null)
            {
                return (video, ".mp4" == extension ? "video/mp4" : "video/webm");
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes the videos of a creation, keeping the rest of its folder.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    public async Task DeleteVideos(int creationId)
    {
        foreach (var extension in VideoExtensions)
        {
            await files.Delete($"{FolderOf(creationId)}/{VideoBaseName}{extension}");
        }
    }

    /// <summary>
    /// Deletes the whole folder of a creation, with its program file and its videos.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    public Task DeleteFolder(int creationId) => files.DeleteFolder(FolderOf(creationId));
}
