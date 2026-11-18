using System.Text;
using System.Text.Json.Nodes;
using kuv_api.Models.Files;

namespace kuv_api.Services;

/// <summary>
/// Keeps the files of the creations in the <see cref="IFileStore"/>. Each creation has its own folder, <c>creations/{id}</c>, holding its
/// program file (<c>program.json</c>) and its preview video, recorded twice: over the dark background of the web
/// app (<c>preview-dark.webm</c> or <c>.mp4</c>) and over the light one (<c>preview-light.webm</c> or <c>.mp4</c>).
/// The database only holds what describes the creation, never its program.
/// </summary>
public sealed class CreationStorage(IFileStore files)
{
    /// <summary>
    /// Name of the program file inside the folder of a creation.
    /// </summary>
    public const string ProgramFileName = "program.json";

    /// <summary>
    /// Start of the name of the videos inside the folder of a creation. Each theme adds its name
    /// (<c>preview-dark</c>); creations saved before there were two keep a single <c>preview</c>.
    /// </summary>
    private const string VideoBaseName = "preview";

    /// <summary>
    /// The themes a video is recorded for, the same as the web app's.
    /// </summary>
    public static readonly string[] VideoThemes = ["dark", "light"];

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
    /// Saves the two uploaded videos of a creation, one per theme, replacing the ones it had.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <param name="dark">
    /// The video recorded over the dark background.
    /// </param>
    /// <param name="light">
    /// The video recorded over the light background.
    /// </param>
    public async Task WriteVideos(int creationId, IFormFile dark, IFormFile light)
    {
        await DeleteVideos(creationId);

        foreach (var (theme, file) in new[] { ("dark", dark), ("light", light) })
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{VideoBaseName}-{theme}{(VideoExtensions.Contains(extension) ? extension : ".webm")}";

            await using var content = file.OpenReadStream();
            await files.Write($"{FolderOf(creationId)}/{fileName}", content);
        }
    }

    /// <summary>
    /// Opens the video of a creation for a theme, whichever of the allowed extensions it was saved with. A creation
    /// saved before there were two videos has a single one, which is given for both themes.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <param name="theme">
    /// One of <see cref="VideoThemes"/>.
    /// </param>
    /// <returns>
    /// The video with its content type, or null when it is not stored.
    /// </returns>
    public async Task<(StoredFile Video, string ContentType)?> OpenVideo(int creationId, string theme)
    {
        foreach (var name in new[] { $"{VideoBaseName}-{theme}", VideoBaseName })
        {
            foreach (var extension in VideoExtensions)
            {
                var video = await files.Open($"{FolderOf(creationId)}/{name}{extension}");

                if (video is not null)
                {
                    return (video, ".mp4" == extension ? "video/mp4" : "video/webm");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes the videos of a creation (those of both themes, and the single one of older creations), keeping the
    /// rest of its folder.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    public async Task DeleteVideos(int creationId)
    {
        foreach (var name in VideoThemes.Select(theme => $"{VideoBaseName}-{theme}").Append(VideoBaseName))
        {
            foreach (var extension in VideoExtensions)
            {
                await files.Delete($"{FolderOf(creationId)}/{name}{extension}");
            }
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
