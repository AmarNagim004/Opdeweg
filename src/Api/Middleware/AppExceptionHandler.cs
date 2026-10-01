using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
            RedisException or RedisTimeoutException => (StatusCodes.Status503ServiceUnavailable, "presence_unavailable", "Realtime presence is temporarily unavailable."),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "client_closed", "The request was cancelled."),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "Something went wrong."),
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
