using System.Reflection;

namespace Application.Abstractions;

/// <summary>
/// Authorization decorator for handlers with return value.
/// Checks permissions before executing the inner handler.
/// </summary>
public class AuthorizedHandler<TRequest, TResponse> : IHandler<TRequest, TResponse>
{
    private readonly IHandler<TRequest, TResponse> _inner;
    private readonly IAuthorizationService _authorizationService;

    public AuthorizedHandler(IHandler<TRequest, TResponse> inner, IAuthorizationService authorizationService)
    {
        _inner = inner;
        _authorizationService = authorizationService;
    }

    public async Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken = default)
    {
        var authResult = await CheckAuthorizationAsync(cancellationToken);
        if (authResult is not null)
            return authResult;

        return await _inner.Handle(request, cancellationToken);
    }

    private async Task<Result<TResponse>?> CheckAuthorizationAsync(CancellationToken cancellationToken)
    {
        var handlerType = _inner.GetType();
        var authorizeAttributes = handlerType.GetCustomAttributes<AuthorizeAttribute>().ToList();

        if (authorizeAttributes.Count == 0)
            return null;

        foreach (var attr in authorizeAttributes)
        {
            if (attr.Permission is not null)
            {
                if (!await _authorizationService.HasPermissionAsync(attr.Permission, cancellationToken))
                    return Result<TResponse>.Forbidden($"Missing permission: {attr.Permission}");
            }

            if (attr.Role is not null)
            {
                if (!await _authorizationService.IsInRoleAsync(attr.Role, cancellationToken))
                    return Result<TResponse>.Forbidden($"Missing role: {attr.Role}");
            }
        }

        return null;
    }
}

/// <summary>
/// Authorization decorator for handlers without return value.
/// Checks permissions before executing the inner handler.
/// </summary>
public class AuthorizedHandler<TRequest> : IHandler<TRequest>
{
    private readonly IHandler<TRequest> _inner;
    private readonly IAuthorizationService _authorizationService;

    public AuthorizedHandler(IHandler<TRequest> inner, IAuthorizationService authorizationService)
    {
        _inner = inner;
        _authorizationService = authorizationService;
    }

    public async Task<Result> Handle(TRequest request, CancellationToken cancellationToken = default)
    {
        var authResult = await CheckAuthorizationAsync(cancellationToken);
        if (authResult is not null)
            return authResult;

        return await _inner.Handle(request, cancellationToken);
    }

    private async Task<Result?> CheckAuthorizationAsync(CancellationToken cancellationToken)
    {
        var handlerType = _inner.GetType();
        var authorizeAttributes = handlerType.GetCustomAttributes<AuthorizeAttribute>().ToList();

        if (authorizeAttributes.Count == 0)
            return null;

        foreach (var attr in authorizeAttributes)
        {
            if (attr.Permission is not null)
            {
                if (!await _authorizationService.HasPermissionAsync(attr.Permission, cancellationToken))
                    return Result.Forbidden($"Missing permission: {attr.Permission}");
            }

            if (attr.Role is not null)
            {
                if (!await _authorizationService.IsInRoleAsync(attr.Role, cancellationToken))
                    return Result.Forbidden($"Missing role: {attr.Role}");
            }
        }

        return null;
    }
}
