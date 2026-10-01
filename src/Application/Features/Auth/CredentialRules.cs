using System.Net.Mail;
using System.Text;
using Opdeweg.Application.Common;
using Opdeweg.Domain.Entities;

namespace Opdeweg.Application.Features.Auth;

public static class CredentialRules
{
    public static string NormalizeEmail(string? email)
    {
        var trimmed = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (trimmed.Length is 0 or > User.EmailMaxLength || !MailAddress.TryCreate(trimmed, out var parsed) || parsed.Address != trimmed)
        {
            throw AppException.Validation("invalid_email", "Enter a valid e-mail address.");
        }

        return trimmed;
    }

    public static void ValidatePassword(string? password, int minLength)
    {
        if (string.IsNullOrEmpty(password) || password.Length < minLength || password.Length > 128)
        {
            throw AppException.Validation("weak_password", $"Use a password of at least {minLength} characters.");
        }
    }

    /// <summary>Trims, collapses whitespace and strips control characters from a display name.</summary>
    public static string NormalizeDisplayName(string? displayName)
    {
        var builder = new StringBuilder();
        var lastWasSpace = false;
        foreach (var ch in (displayName ?? string.Empty).Trim())
        {
            if (char.IsControl(ch) || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.Format)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            builder.Append(ch);
            lastWasSpace = false;
        }

        var normalized = builder.ToString().Trim();
        if (normalized.Length is < User.DisplayNameMinLength or > User.DisplayNameMaxLength)
        {
            throw AppException.Validation(
                "invalid_display_name",
                $"Display names are {User.DisplayNameMinLength}–{User.DisplayNameMaxLength} characters.");
        }

        return normalized;
    }

    public static string NormalizeAvatarUrl(string avatarUrl)
    {
        var trimmed = avatarUrl.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed.Length > User.AvatarUrlMaxLength ||
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw AppException.Validation("invalid_avatar_url", "Avatar URLs must be https links.");
        }

        return uri.ToString();
    }
}
