namespace Application.Abstractions;

public interface IAuthorizationService
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);
    Task<bool> HasAnyPermissionAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);
    Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default);
    string? GetUserId();
}
