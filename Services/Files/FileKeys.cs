namespace kuv_api.Services;

/// <summary>
/// Checks the keys of <see cref="IFileStore"/>, so a key can never point outside the place the files are kept.
/// </summary>
public static class FileKeys
{
    /// <summary>
    /// Makes sure a key is a plain relative path: not empty, not rooted, no backslashes and no <c>.</c> or <c>..</c>
    /// parts.
    /// </summary>
    /// <param name="key">
    /// The key to check.
    /// </param>
    /// <returns>
    /// The same key, so the call can be used inside an expression.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The key is not a plain relative path. Keys are made by the API itself, so this is a bug, not a user error.
    /// </exception>
    public static string Check(string key)
    {
        var invalid =
            string.IsNullOrWhiteSpace(key) ||
            key.StartsWith('/') ||
            key.Contains('\\') ||
            key.Contains('\0') ||
            key.Split('/').Any(part => part is "" or "." or "..");

        if (invalid)
        {
            throw new ArgumentException($"'{key}' is not a valid file key.", nameof(key));
        }

        return key;
    }
}
