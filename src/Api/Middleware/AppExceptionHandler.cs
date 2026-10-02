using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Opdeweg.Application.Common;
using StackExchange.Redis;

namespace Opdeweg.Api.Middleware;

/// <summary>Maps expected application errors to RFC 7807 problem details with a stable <c>code</c>.</summary>
internal sealed partial class AppExceptionHandler(IProblemDetailsService problemDetails, ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            AppException app => (StatusFor(app.Kind), app.Code, app.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "conflict", "Er veranderde net tegelijk iets anders. Probeer het nog een keer."),
            RedisException or RedisTimeoutException => (StatusCodes.Status503ServiceUnavailable, "presence_unavailable", "Live-gegevens zijn even niet beschikbaar."),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "client_closed", "Het verzoek is afgebroken."),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "Er ging iets mis."),
        };

        if (status >= 500)
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = $"https://opdeweg.app/errors/{code}",
                Extensions = { ["code"] = code },
            },
        });
    }

    private static int StatusFor(AppErrorKind kind) => kind switch
    {
        AppErrorKind.Validation => StatusCodes.Status400BadRequest,
        AppErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        AppErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        AppErrorKind.NotFound => StatusCodes.Status404NotFound,
        AppErrorKind.Conflict => StatusCodes.Status409Conflict,
        AppErrorKind.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        AppErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest,
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);
}
