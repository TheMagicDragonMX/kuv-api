using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;
using kuv_api.Models.Exceptions;

namespace kuv_api.Services;

/// <summary>
/// Checks that a document is a valid program file against <c>Schemas/program-file.schema.json</c>, which is embedded in
/// the API. The schema fixes the structure (version, program, scripts, triggers, environment); the fields of each block
/// are checked by the engine of the client.
/// </summary>
public sealed class ProgramFileValidator
{
    /// <summary>
    /// Name the schema is embedded with.
    /// </summary>
    private const string SchemaResource = "program-file.schema.json";

    /// <summary>
    /// The schema every program file must follow.
    /// </summary>
    private readonly JsonSchema schema = LoadSchema();

    /// <summary>
    /// Parses the text of a program file and checks it.
    /// </summary>
    /// <param name="text">
    /// The JSON text of the program file.
    /// </param>
    /// <returns>
    /// The parsed document.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The text is not JSON, or the document does not follow the schema. The message says where.
    /// </exception>
    public JsonNode Parse(string text)
    {
        JsonNode? document;

        try
        {
            document = JsonNode.Parse(text);
        }

        catch (System.Text.Json.JsonException)
        {
            throw new BadRequestException("El archivo del programa no es un JSON válido.");
        }

        if (document is null)
        {
            throw new BadRequestException("El archivo del programa no puede ser null.");
        }

        Validate(document);

        return document;
    }

    /// <summary>
    /// Checks a document against the schema.
    /// </summary>
    /// <param name="document">
    /// The program file.
    /// </param>
    /// <exception cref="BadRequestException">
    /// The document does not follow the schema. The message says where.
    /// </exception>
    public void Validate(JsonNode document)
    {
        var results = schema.Evaluate(System.Text.Json.JsonSerializer.SerializeToElement(document), new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (results.IsValid)
        {
            return;
        }

        // The deepest error is the most precise one; the ones above it only say that something below failed. The
        // conditions of an if are not errors
        var problem = (results.Details ?? [])
            .Where(detail => detail.Errors is { Count: > 0 } && !detail.EvaluationPath.ToString().Contains("/if/"))
            .OrderByDescending(detail => detail.InstanceLocation.ToString().Length)
            .Select(detail => $"{detail.InstanceLocation}: {detail.Errors!.First().Value}")
            .FirstOrDefault();

        throw new BadRequestException($"El archivo del programa no es válido. {problem}");
    }

    /// <summary>
    /// Reads the embedded schema.
    /// </summary>
    private static JsonSchema LoadSchema()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SchemaResource)
            ?? throw new InvalidOperationException($"The schema {SchemaResource} is not embedded in the API.");
        using var reader = new StreamReader(stream);

        return JsonSchema.FromText(reader.ReadToEnd());
    }
}
