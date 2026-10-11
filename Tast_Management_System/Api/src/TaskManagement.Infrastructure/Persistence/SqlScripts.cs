using System.Reflection;

namespace TaskManagement.Infrastructure.Persistence;

/// <summary>
/// Reads .sql files embedded in this assembly (see EmbeddedResource in the csproj).
/// Keeping SQL in real .sql files means it can be reviewed and run as-is in SSMS / sqlcmd.
/// </summary>
internal static class SqlScripts
{
    public static string Read(string fileName)
    {
        var assembly = typeof(SqlScripts).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith("." + fileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded SQL script '{fileName}' was not found.");

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
