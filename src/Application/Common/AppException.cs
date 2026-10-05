namespace Opdeweg.Application.Common;

public enum AppErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Unprocessable,
    Unavailable,
}

/// <summary>
/// An expected, client-facing failure. The API maps <see cref="Kind"/> to an HTTP status and
/// exposes <see cref="Code"/> as a stable, machine-readable error code.
/// </summary>
public sealed class AppException : Exception
{
    public AppException(AppErrorKind kind, string code, string message)
        : base(message)
    {
        Kind = kind;
        Code = code;
    }

    public AppException()
        : this(AppErrorKind.Validation, "error", "An error occurred.")
    {
    }

    public AppException(string message)
        : this(AppErrorKind.Validation, "error", message)
    {
    }

    public AppException(string message, Exception innerException)
        : base(message, innerException)
    {
        Kind = AppErrorKind.Validation;
        Code = "error";
    }

    public AppErrorKind Kind { get; }

    public string Code { get; }

    public static AppException Validation(string code, string message) => new(AppErrorKind.Validation, code, message);

    public static AppException Unauthorized(string code, string message) => new(AppErrorKind.Unauthorized, code, message);

    public static AppException NotFound(string code, string message) => new(AppErrorKind.NotFound, code, message);

    public static AppException Conflict(string code, string message) => new(AppErrorKind.Conflict, code, message);

    public static AppException Unprocessable(string code, string message) => new(AppErrorKind.Unprocessable, code, message);
}
