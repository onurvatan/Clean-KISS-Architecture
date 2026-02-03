using Application.Abstractions;

namespace Infrastructure.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUser _currentUser;

    public AuthorizationService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public string? GetUserId() => _currentUser.Id;

    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        var hasPermission = _currentUser.Permissions.Contains(permission);
        return Task.FromResult(hasPermission);
    }

    public Task<bool> HasAnyPermissionAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        var hasAny = permissions.Any(p => _currentUser.Permissions.Contains(p));
        return Task.FromResult(hasAny);
    }

    public Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var isInRole = _currentUser.Roles.Contains(role);
        return Task.FromResult(isInRole);
    }
}
