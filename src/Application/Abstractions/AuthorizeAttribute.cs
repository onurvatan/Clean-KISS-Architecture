namespace Application.Abstractions;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AuthorizeAttribute : Attribute
{
    public string? Permission { get; init; }
    public string? Role { get; init; }

    public AuthorizeAttribute() { }

    public AuthorizeAttribute(string permission)
    {
        Permission = permission;
    }

    public static AuthorizeAttribute ForRole(string role) => new() { Role = role };
}
