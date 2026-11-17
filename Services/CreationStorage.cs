using System.Text.Json.Nodes;

namespace kuv_api.Services;

/// <summary>
/// Keeps the files of the creations. Each creation has its own folder, <c>uploads/creations/{id}</c>, holding its
/// program file (<c>program.json</c>) and its video (<c>video.webm</c> or <c>video.mp4</c>). The database only
/// holds what describes the creation, never its program.
/// </summary>
public sealed class CreationStorage(IWebHostEnvironment environment)
{
    /// <summary>
    /// Name of the program file inside the folder of a creation.
    /// </summary>
    public const string ProgramFileName = "program.json";

    /// <summary>
    /// Name, without extension, of the video inside the folder of a creation.
    /// </summary>
    private const string VideoBaseName = "video";

    /// <summary>
    /// The extensions a video can have. Anything else is stored as <c>.webm</c>, so the name of the uploaded file
    /// never decides what is written to disk.
    /// </summary>
    private static readonly string[] VideoExtensions = [".webm", ".mp4"];

    /// <summary>
    /// The folder that holds the folders of all the creations.
    /// </summary>
    public string Root { get; } = Path.Combine(environment.ContentRootPath, "uploads", "creations");

    /// <summary>
    /// The folder of a creation. It may not exist yet.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    public string FolderOf(int creationId) => Path.Combine(Root, creationId.ToString());

    /// <summary>
    /// Writes the program file of a creation, replacing the one it had. The folder is created when needed.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <param name="programFile">
    /// The validated program file.
    /// </param>
    public async Task WriteProgramFile(int creationId, JsonNode programFile)
    {
        var folder = FolderOf(creationId);
        Directory.CreateDirectory(folder);

        // Written aside and then moved over, so a failure never leaves a half-written program file
        var path = Path.Combine(folder, ProgramFileName);
        var temporary = path + ".tmp";

        await File.WriteAllTextAsync(temporary, programFile.ToJsonString());
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>
    /// Reads the program file of a creation.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <returns>
    /// The program file, or null when the creation has none on disk.
    /// </returns>
    public async Task<JsonNode?> ReadProgramFile(int creationId)
    {
        var path = Path.Combine(FolderOf(creationId), ProgramFileName);

        if (!File.Exists(path))
        {
            return null;
        }

        return JsonNode.Parse(await File.ReadAllTextAsync(path));
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
    /// <returns>
    /// The name of the video file inside the folder of the creation.
    /// </returns>
    public async Task<string> WriteVideo(int creationId, IFormFile file)
    {
        var folder = FolderOf(creationId);
        Directory.CreateDirectory(folder);
        DeleteVideos(creationId);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = VideoBaseName + (VideoExtensions.Contains(extension) ? extension : ".webm");

        await using var stream = File.Create(Path.Combine(folder, fileName));
        await file.CopyToAsync(stream);

        return fileName;
    }

    /// <summary>
    /// The path of the video of a creation.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    /// <param name="fileName">
    /// The name of the video file stored in the database. Only its name is used, never a path in it.
    /// </param>
    public string VideoPath(int creationId, string fileName) =>
        Path.Combine(FolderOf(creationId), Path.GetFileName(fileName));

    /// <summary>
    /// Deletes the videos of a creation, keeping the rest of its folder.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    public void DeleteVideos(int creationId)
    {
        var folder = FolderOf(creationId);

        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var extension in VideoExtensions)
        {
            File.Delete(Path.Combine(folder, VideoBaseName + extension));
        }
    }

    /// <summary>
    /// Deletes the whole folder of a creation, with its program file and its videos.
    /// </summary>
    /// <param name="creationId">
    /// The creation.
    /// </param>
    public void DeleteFolder(int creationId)
    {
        var folder = FolderOf(creationId);

        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
