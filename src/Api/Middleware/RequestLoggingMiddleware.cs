using System.Diagnostics;
using Opdeweg.Api.Extensions;

namespace Opdeweg.Api.Middleware;

/// <summary>
/// Privacy-conscious access log: method, path (never the query string, which may carry the hub
/// access token), status, duration and the caller's user ID. Bodies — and thus coordinates — are never logged.
/// </summary>
internal sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var path = context.Request.Path.Value ?? "/";
            if (!path.StartsWith("/health", StringComparison.Ordinal))
            {
                LogRequest(logger, context.Request.Method, path, context.Response.StatusCode, elapsed, context.User.GetUserIdOrNull());
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:F1} ms (user {UserId})")]
    private static partial void LogRequest(ILogger logger, string method, string path, int statusCode, double elapsedMs, Guid? userId);
}
