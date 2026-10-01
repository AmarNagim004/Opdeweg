namespace Opdeweg.Domain.Enums;

/// <summary>Login providers. Social providers are modelled now so they can be added without schema changes.</summary>
public enum AuthProvider
{
    Password = 1,
    Apple = 2,
    Google = 3,
}
