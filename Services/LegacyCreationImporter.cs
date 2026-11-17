using System.Globalization;
using System.Text.Json.Nodes;
using kuv_api.Data;
using kuv_api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Moves the creations stored the old way into their own folders. Before, the program and the environment were
/// columns of the database and the video was a loose file in <c>uploads/creations</c>. For each creation that still
/// has them, this writes its <c>program.json</c>, moves its video into its folder and empties the old columns. It runs
/// at startup and does nothing once every creation has been moved, so it is safe to run again.
/// </summary>
/// <remarks>
/// A creation whose old program does not follow the schema is left as it is and reported in the log.
/// </remarks>
public sealed class LegacyCreationImporter(
    KuvDbContext dbContext,
    CreationStorage storage,
    ProgramFileValidator validator,
    ILogger<LegacyCreationImporter> logger)
{
    /// <summary>
    /// Pixels on one face: the picture of a face has this many colors.
    /// </summary>
    private const int FacePixels = 64 * 64;

    /// <summary>
    /// Moves every creation that still has its program in the database.
    /// </summary>
    public async Task Run()
    {
        var creations = await dbContext.Creations
            .Where(creation => creation.LegacyProgramJson != null)
            .ToListAsync();

        foreach (var creation in creations)
        {
            try
            {
                await Import(creation);
                logger.LogInformation("Creation {CreationId} moved to its own folder.", creation.Id);
            }

            catch (Exception exception)
            {
                logger.LogError(exception, "Creation {CreationId} could not be moved to its own folder and was left as it is.", creation.Id);
            }
        }
    }

    /// <summary>
    /// Moves one creation: writes its program file, moves its video and empties its old columns.
    /// </summary>
    private async Task Import(Creation creation)
    {
        var programFile = new JsonObject
        {
            ["version"] = 1,
            ["program"] = JsonNode.Parse(creation.LegacyProgramJson!)
        };

        if (!string.IsNullOrWhiteSpace(creation.LegacyEnvironmentJson) && JsonNode.Parse(creation.LegacyEnvironmentJson) is JsonObject environment)
        {
            ConvertImages(environment);
            programFile["environment"] = environment;
        }

        validator.Validate(programFile);

        await storage.WriteProgramFile(creation.Id, programFile);
        MoveVideo(creation);

        creation.LegacyProgramJson = null;
        creation.LegacyEnvironmentJson = null;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Moves the loose video of a creation into its folder. A video that is not on disk is forgotten.
    /// </summary>
    private void MoveVideo(Creation creation)
    {
        if (string.IsNullOrWhiteSpace(creation.VideoFileName))
        {
            return;
        }

        var loose = Path.Combine(storage.Root, Path.GetFileName(creation.VideoFileName));

        if (!File.Exists(loose))
        {
            creation.VideoFileName = null;
            creation.VideoMimeType = null;

            return;
        }

        var extension = Path.GetExtension(loose).ToLowerInvariant();
        var fileName = "video" + (".mp4" == extension ? extension : ".webm");

        Directory.CreateDirectory(storage.FolderOf(creation.Id));
        File.Move(loose, storage.VideoPath(creation.Id, fileName), overwrite: true);
        creation.VideoFileName = fileName;
    }

    /// <summary>
    /// Writes the pictures of an old environment the way the schema wants them. They were saved straight from the
    /// browser's pixel buffer, as numbers (or, worse, as an object with the position as the key); now each is a list
    /// of colors written as #RRGGBB, like the ones in the blocks.
    /// </summary>
    private static void ConvertImages(JsonObject environment)
    {
        if (environment["background"] is JsonObject background)
        {
            ConvertFaceImages(background);
        }

        if (environment["backgrounds"] is JsonArray backgrounds)
        {
            foreach (var named in backgrounds.OfType<JsonObject>())
            {
                if (named["images"] is JsonObject images)
                {
                    ConvertFaceImages(images);
                }
            }
        }
    }

    /// <summary>
    /// Converts the picture of each face of an object keyed by face.
    /// </summary>
    private static void ConvertFaceImages(JsonObject faces)
    {
        foreach (var face in faces.ToList())
        {
            faces[face.Key] = ConvertImage(face.Value);
        }
    }

    /// <summary>
    /// Converts one old picture (a list of numbers or an object keyed by position) to a list of #RRGGBB colors. What
    /// is not a picture is returned as it is, for the schema to refuse.
    /// </summary>
    private static JsonNode? ConvertImage(JsonNode? image)
    {
        var colors = new long[FacePixels];

        switch (image)
        {
            case JsonArray list when FacePixels == list.Count:
                for (var index = 0; index < list.Count; index++)
                {
                    if (!TryReadColor(list[index], out colors[index]))
                    {
                        return image;
                    }
                }

                break;

            case JsonObject indexed when FacePixels == indexed.Count:
                foreach (var (key, value) in indexed)
                {
                    if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
                        index >= FacePixels ||
                        !TryReadColor(value, out colors[index]))
                    {
                        return image;
                    }
                }

                break;

            default:
                return image;
        }

        return new JsonArray(colors.Select(color => (JsonNode)JsonValue.Create($"#{color & 0xFFFFFF:x6}")!).ToArray());
    }

    /// <summary>
    /// Reads a color saved as a number.
    /// </summary>
    private static bool TryReadColor(JsonNode? node, out long color)
    {
        color = 0;

        return node is JsonValue value && value.TryGetValue(out color) && color is >= 0 and <= 0xFFFFFF;
    }
}
