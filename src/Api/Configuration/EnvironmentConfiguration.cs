namespace Opdeweg.Api.Configuration;

/// <summary>
/// Maps the documented flat environment variables (DATABASE_CONNECTION_STRING, JWT_SECRET, ...)
/// onto configuration keys, and loads a local <c>.env</c> file in Development.
/// </summary>
internal static class EnvironmentConfiguration
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["DATABASE_CONNECTION_STRING"] = "ConnectionStrings:Database",
        ["REDIS_CONNECTION_STRING"] = "ConnectionStrings:Redis",
        ["JWT_SECRET"] = "Jwt:Secret",
        ["JWT_ISSUER"] = "Jwt:Issuer",
        ["JWT_AUDIENCE"] = "Jwt:Audience",
        ["LIVEKIT_URL"] = "LiveKit:Url",
        ["LIVEKIT_API_URL"] = "LiveKit:ApiUrl",
        ["LIVEKIT_API_KEY"] = "LiveKit:ApiKey",
        ["LIVEKIT_API_SECRET"] = "LiveKit:ApiSecret",
    };

    public static void Apply(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            LoadDotEnv(builder.Environment.ContentRootPath);
        }

        var values = new Dictionary<string, string?>();
        foreach (var (variable, key) in Aliases)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
            }
        }

        builder.Configuration.AddInMemoryCollection(values);
    }

    /// <summary>Loads KEY=VALUE pairs from the nearest .env (walking up to the repository root). Existing variables win.</summary>
    private static void LoadDotEnv(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".env");
            if (File.Exists(path))
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#') || !line.Contains('=', StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var separator = line.IndexOf('=', StringComparison.Ordinal);
                    var name = line[..separator].Trim();
                    var value = line[(separator + 1)..].Trim().Trim('"');
                    if (Environment.GetEnvironmentVariable(name) is null)
                    {
                        Environment.SetEnvironmentVariable(name, value);
                    }
                }

                return;
            }

            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return;
            }
        }
    }
}
