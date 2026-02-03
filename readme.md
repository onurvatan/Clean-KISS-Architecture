# Clean KISS Architecture

A simple, maintainable, domain-first architecture designed around clarity, testability, and real-world development needs.

> No ceremony. No over-engineering. No "enterprise theatre."  
> Just clean boundaries, predictable behaviour, and code you can debug locally.

---

## 📁 Folder Structure

```
/src
  /Domain
    /Entities
      Student.cs
      Course.cs
      Enrollment.cs
    /ValueObjects
      Email.cs
      Money.cs
      DateRange.cs
    /Interfaces
      IStudentRepository.cs
      ICourseRepository.cs
    /Events
      StudentRegistered.cs (optional)

  /Application
    /DTOs
      StudentDto.cs
      CourseDto.cs
    /Extensions
      StudentExtensions.cs
      CourseExtensions.cs
    /Handlers
      /RegisterStudent
        RegisterStudentCommand.cs
        RegisterStudentHandler.cs
      /GetStudent
        GetStudentQuery.cs
        GetStudentHandler.cs
      /DeleteStudent
        DeleteStudentCommand.cs
        DeleteStudentHandler.cs
    /Services
      IEmailService.cs
      IClock.cs
      ICacheService.cs
    /Abstractions
      IHandler.cs
      Result.cs
      IUnitOfWork.cs
      IAuthorizationService.cs
      ICurrentUser.cs
      AuthorizeAttribute.cs
      AuthorizedHandler.cs
      Permissions.cs
      IIdempotencyService.cs

  /Infrastructure
    /Persistence
      /EF
        AppDbContext.cs
        StudentConfiguration.cs
        CourseConfiguration.cs
        UnitOfWork.cs
      /Repositories
        StudentRepository.cs
        CourseRepository.cs
    /Services
      EmailService.cs
      Clock.cs
      CacheService.cs
      CurrentUser.cs
      AuthorizationService.cs
      IdempotencyService.cs
      RedisIdempotencyService.cs

  /API
    /Controllers
      StudentsController.cs
      CoursesController.cs
    /Middleware
      ExceptionMiddleware.cs
      IdempotencyMiddleware.cs
      CorrelationIdMiddleware.cs
    /Extensions
      ResultExtensions.cs
    /Models
      RegisterStudentRequest.cs
      StudentResponse.cs

/tests
  /Domain.Tests
    /ValueObjects
      EmailTests.cs
      NameTests.cs
    /Entities
      StudentTests.cs

  /Application.Tests
    /Handlers
      RegisterStudentHandlerTests.cs
      GetStudentHandlerTests.cs
      DeleteStudentHandlerTests.cs

  /Infrastructure.Tests
    /Repositories
      StudentRepositoryTests.cs

  /API.Tests
    /Controllers
      StudentsControllerTests.cs
```

---

## 🎯 Architectural Principles

### 1. Domain is pure

- No EF Core attributes
- No DTOs
- No API concerns
- Only business rules, invariants, and value objects
- **Value objects validate their own invariants** (format, structure, constraints)

### 2. Application orchestrates

- Handlers implement `IHandler<TRequest, TResponse>`
- **Business validation lives in handlers** (uniqueness, authorization, state checks)
- Mapping done via extension methods
- DTOs defined here
- Returns `Result<T>` instead of throwing exceptions

### 3. Infrastructure handles persistence

- EF Core DbContext
- Repository implementations
- Unit of Work implementation
- External services (email, clock, etc.)

### 4. API is thin

- Controllers only accept requests
- Call handlers
- Map `Result<T>` to appropriate HTTP responses
- Global exception middleware as safety net
- No business logic

---

## 🎯 Validation Philosophy

| Layer             | Validates             | Examples                                                |
| ----------------- | --------------------- | ------------------------------------------------------- |
| **Value Objects** | Structural invariants | Email format, Money non-negative, DateRange start < end |
| **Handlers**      | Business rules        | Email uniqueness, user authorization, enrollment limits |

This keeps validation **co-located with the data it protects** — no scattered validation logic.

---

## 📦 Result Pattern

### Generic Result (with value)

```csharp
public class Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public int StatusCode { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    private Result(T? value, string? error, int statusCode)
    {
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    // Success
    public static Result<T> Success(T value)
        => new(value, null, 200);

    public static Result<T> Created(T value)
        => new(value, null, 201);

    // Client errors
    public static Result<T> BadRequest(string error)
        => new(default, error, 400);

    public static Result<T> NotFound(string error)
        => new(default, error, 404);

    public static Result<T> Conflict(string error)
        => new(default, error, 409);

    public static Result<T> Forbidden(string error)
        => new(default, error, 403);

    public static Result<T> Unauthorized(string error)
        => new(default, error, 401);

    // Pattern matching
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
```

### Non-Generic Result (no value)

```csharp
public class Result
{
    public string? Error { get; }
    public int StatusCode { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    private Result(string? error, int statusCode)
    {
        Error = error;
        StatusCode = statusCode;
    }

    // Success
    public static Result Success() => new(null, 200);
    public static Result NoContent() => new(null, 204);

    // Client errors
    public static Result BadRequest(string error) => new(error, 400);
    public static Result NotFound(string error) => new(error, 404);
    public static Result Conflict(string error) => new(error, 409);
    public static Result Forbidden(string error) => new(error, 403);
    public static Result Unauthorized(string error) => new(error, 401);
}
```

No exceptions for expected failures. Clean, predictable control flow with built-in HTTP semantics.

---

## 🧩 Generic Handler Interface

```csharp
public interface IHandler<TRequest, TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken = default);
}

// For handlers that don't return a value
public interface IHandler<TRequest>
{
    Task<Result> Handle(TRequest request, CancellationToken cancellationToken = default);
}
```

Simple, generic, testable, with cancellation support.

---

## � Authorization Layer

The authorization layer sits **before** handlers to verify that the current user's token has permission to execute the requested operation.

### Authorization Interfaces

```csharp
// Application/Abstractions/IAuthorizationService.cs
public interface IAuthorizationService
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);
    Task<bool> HasAnyPermissionAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);
    Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default);
    string? GetUserId();
}
```

```csharp
// Application/Abstractions/ICurrentUser.cs
public interface ICurrentUser
{
    string? Id { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsAuthenticated { get; }
}
```

### Authorize Attribute for Handlers

```csharp
// Application/Abstractions/AuthorizeAttribute.cs
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AuthorizeAttribute : Attribute
{
    public string? Permission { get; }
    public string? Role { get; }

    public AuthorizeAttribute() { }

    public AuthorizeAttribute(string permission)
    {
        Permission = permission;
    }

    public static AuthorizeAttribute ForRole(string role) => new() { Role = role };
}
```

### Permissions Constants

```csharp
// Application/Abstractions/Permissions.cs
public static class Permissions
{
    public static class Students
    {
        public const string View = "students:view";
        public const string Create = "students:create";
        public const string Update = "students:update";
        public const string Delete = "students:delete";
    }

    public static class Courses
    {
        public const string View = "courses:view";
        public const string Create = "courses:create";
        public const string Update = "courses:update";
        public const string Delete = "courses:delete";
    }
}
```

### Authorization Decorator

```csharp
// Application/Abstractions/AuthorizedHandler.cs
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
        var handlerType = _inner.GetType();
        var authorizeAttributes = handlerType.GetCustomAttributes<AuthorizeAttribute>().ToList();

        if (authorizeAttributes.Count == 0)
            return await _inner.Handle(request, cancellationToken);

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

        return await _inner.Handle(request, cancellationToken);
    }
}

// Non-generic version
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
        var handlerType = _inner.GetType();
        var authorizeAttributes = handlerType.GetCustomAttributes<AuthorizeAttribute>().ToList();

        if (authorizeAttributes.Count == 0)
            return await _inner.Handle(request, cancellationToken);

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

        return await _inner.Handle(request, cancellationToken);
    }
}
```

### Infrastructure Implementation

```csharp
// Infrastructure/Services/CurrentUser.cs
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? Id => User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? Email => User?.FindFirstValue(ClaimTypes.Email);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public IReadOnlyList<string> Permissions =>
        User?.FindAll("permission").Select(c => c.Value).ToList() ?? [];
}
```

```csharp
// Infrastructure/Services/AuthorizationService.cs
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
```

### Handler Usage with Authorization

```csharp
[Authorize(Permissions.Students.Create)]
public class RegisterStudentHandler : IHandler<RegisterStudentCommand, StudentDto>
{
    // ... handler implementation
}

[Authorize(Permissions.Students.View)]
public class GetStudentHandler : IHandler<GetStudentQuery, StudentDto>
{
    // ... handler implementation
}

[Authorize(Permissions.Students.Delete)]
public class DeleteStudentHandler : IHandler<DeleteStudentCommand>
{
    // ... handler implementation
}
```

### DI Registration with Authorization

```csharp
// Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register concrete handlers
        services.AddScoped<RegisterStudentHandler>();
        services.AddScoped<GetStudentHandler>();
        services.AddScoped<DeleteStudentHandler>();

        // Register authorized wrappers
        services.AddScoped<IHandler<RegisterStudentCommand, StudentDto>>(sp =>
            new AuthorizedHandler<RegisterStudentCommand, StudentDto>(
                sp.GetRequiredService<RegisterStudentHandler>(),
                sp.GetRequiredService<IAuthorizationService>()));

        services.AddScoped<IHandler<GetStudentQuery, StudentDto>>(sp =>
            new AuthorizedHandler<GetStudentQuery, StudentDto>(
                sp.GetRequiredService<GetStudentHandler>(),
                sp.GetRequiredService<IAuthorizationService>()));

        services.AddScoped<IHandler<DeleteStudentCommand>>(sp =>
            new AuthorizedHandler<DeleteStudentCommand>(
                sp.GetRequiredService<DeleteStudentHandler>(),
                sp.GetRequiredService<IAuthorizationService>()));

        return services;
    }
}
```

```csharp
// Infrastructure/DependencyInjection.cs (add to existing)
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUser, CurrentUser>();
services.AddScoped<IAuthorizationService, AuthorizationService>();
```

### Authorization Flow

```
Request → Controller → AuthorizedHandler → [Permission Check] → Handler → Result
                                ↓
                         Forbidden (403) if unauthorized
```

| Check Type | Attribute Example                 | Failure Result          |
| ---------- | --------------------------------- | ----------------------- |
| Permission | `[Authorize("students:create")]`  | `Result.Forbidden(...)` |
| Role       | `[Authorize] { Role = "Admin" }`  | `Result.Forbidden(...)` |
| Multiple   | Multiple `[Authorize]` attributes | All must pass           |
| No Auth    | No `[Authorize]` attribute        | Passes through          |

> Authorization is checked **before** the handler executes — fail fast, no wasted work.

---

## �🔄 Unit of Work

### Interface

```csharp
// Application/Abstractions/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

### Implementation

```csharp
// Infrastructure/Persistence/EF/UnitOfWork.cs
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
```

---

## 📦 Caching

### Interface

```csharp
// Application/Services/ICacheService.cs
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
}
```

### Implementation (using IMemoryCache)

```csharp
// Infrastructure/Services/CacheService.cs
public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public CacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
        };
        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out T? value) && value is not null)
            return value;

        value = await factory();

        if (value is not null)
            await SetAsync(key, value, expiration, cancellationToken);

        return value;
    }
}
```

### Implementation (using Redis)

```csharp
// Infrastructure/Services/RedisCacheService.cs
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public RedisCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var data = await _cache.GetStringAsync(key, cancellationToken);
        return data is null ? default : JsonSerializer.Deserialize<T>(data);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
        };
        var data = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, data, options, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(key, cancellationToken);

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory();

        if (value is not null)
            await SetAsync(key, value, expiration, cancellationToken);

        return value;
    }
}
```

### Cache Key Helper

```csharp
// Application/Extensions/CacheKeys.cs
public static class CacheKeys
{
    public static string Student(Guid id) => $"student:{id}";
    public static string StudentByEmail(string email) => $"student:email:{email}";
    public static string AllStudents => "students:all";
}
```

### Usage in Query Handler

```csharp
public class GetStudentHandler : IHandler<GetStudentQuery, StudentDto>
{
    private readonly IStudentRepository _repository;
    private readonly ICacheService _cache;

    public GetStudentHandler(IStudentRepository repository, ICacheService cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<Result<StudentDto>> Handle(GetStudentQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Student(query.Id);

        var dto = await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            var student = await _repository.GetByIdAsync(query.Id, cancellationToken);
            return student?.ToDto();
        }, TimeSpan.FromMinutes(10), cancellationToken);

        if (dto is null)
            return Result<StudentDto>.NotFound("Student not found");

        return Result<StudentDto>.Success(dto);
    }
}
```

### Cache Invalidation in Command Handler

```csharp
public class RegisterStudentHandler : IHandler<RegisterStudentCommand, StudentDto>
{
    private readonly IStudentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public RegisterStudentHandler(
        IStudentRepository repository,
        IUnitOfWork unitOfWork,
        ICacheService cache)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<Result<StudentDto>> Handle(RegisterStudentCommand command, CancellationToken cancellationToken = default)
    {
        var exists = await _repository.ExistsByEmailAsync(command.Email, cancellationToken);
        if (exists)
            return Result<StudentDto>.Conflict("A student with this email already exists");

        var email = new Email(command.Email);
        var student = new Student(command.Name, email);

        await _repository.AddAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate list cache
        await _cache.RemoveAsync(CacheKeys.AllStudents, cancellationToken);

        return Result<StudentDto>.Created(student.ToDto());
    }
}
```

### Caching Strategy

| Operation       | Cache Action                 | Why                  |
| --------------- | ---------------------------- | -------------------- |
| **Query (Get)** | Cache result                 | Reduce database hits |
| **Create**      | Invalidate list cache        | List is now stale    |
| **Update**      | Invalidate item + list cache | Both are stale       |
| **Delete**      | Invalidate item + list cache | Both are stale       |

> Cache in **query handlers**, invalidate in **command handlers**.

---

## 🧪 Example Value Object (with self-validation)

```csharp
public sealed class Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");

        if (!value.Contains("@"))
            throw new ArgumentException("Email is invalid");

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
```

The value object **protects its own invariants**. If it exists, it's valid.

---

## 🧪 Example Handlers

### Handler with return value

```csharp
public class RegisterStudentHandler : IHandler<RegisterStudentCommand, StudentDto>
{
    private readonly IStudentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterStudentHandler(IStudentRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<StudentDto>> Handle(RegisterStudentCommand command, CancellationToken cancellationToken = default)
    {
        // Business validation: check uniqueness
        var exists = await _repository.ExistsByEmailAsync(command.Email, cancellationToken);
        if (exists)
            return Result<StudentDto>.Conflict("A student with this email already exists");

        // Value object validates format (throws if invalid)
        var email = new Email(command.Email);
        var student = new Student(command.Name, email);

        await _repository.AddAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<StudentDto>.Created(student.ToDto());
    }
}
```

### Handler without return value

```csharp
public class DeleteStudentHandler : IHandler<DeleteStudentCommand>
{
    private readonly IStudentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteStudentHandler(IStudentRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteStudentCommand command, CancellationToken cancellationToken = default)
    {
        var student = await _repository.GetByIdAsync(command.Id, cancellationToken);
        if (student is null)
            return Result.NotFound("Student not found");

        await _repository.DeleteAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.NoContent();
    }
}
```

- **Handler** checks business rules
- **Value object** validates structure
- **Result** returns success or failure — no exception-driven flow
- **UnitOfWork** explicitly saves changes

---

## 🔄 Mapping via Extensions

```csharp
public static class StudentExtensions
{
    public static StudentDto ToDto(this Student s)
        => new StudentDto(s.Id, s.Name.Value, s.Email.Value);
}
```

- No AutoMapper.
- No magic.
- Just clean, explicit mapping.

---

## 🌐 API Result Handling

### Result Extension for Controllers

```csharp
// API/Extensions/ResultExtensions.cs
public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller, string? actionName = null, Func<T, object>? routeValues = null)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode switch
            {
                201 when actionName is not null && routeValues is not null
                    => controller.CreatedAtAction(actionName, routeValues(result.Value!), result.Value),
                201 => controller.StatusCode(201, result.Value),
                204 => controller.NoContent(),
                _ => controller.Ok(result.Value)
            };
        }

        return result.StatusCode switch
        {
            400 => controller.BadRequest(new { error = result.Error }),
            401 => controller.Unauthorized(new { error = result.Error }),
            404 => controller.NotFound(new { error = result.Error }),
            403 => controller.Forbid(),
            409 => controller.Conflict(new { error = result.Error }),
            _ => controller.BadRequest(new { error = result.Error })
        };
    }

    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode switch
            {
                204 => controller.NoContent(),
                _ => controller.Ok()
            };
        }

        return result.StatusCode switch
        {
            400 => controller.BadRequest(new { error = result.Error }),
            401 => controller.Unauthorized(new { error = result.Error }),
            404 => controller.NotFound(new { error = result.Error }),
            403 => controller.Forbid(),
            409 => controller.Conflict(new { error = result.Error }),
            _ => controller.BadRequest(new { error = result.Error })
        };
    }
}
```

### Controller Usage

```csharp
[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IHandler<RegisterStudentCommand, StudentDto> _registerHandler;
    private readonly IHandler<GetStudentQuery, StudentDto> _getHandler;
    private readonly IHandler<DeleteStudentCommand> _deleteHandler;

    public StudentsController(
        IHandler<RegisterStudentCommand, StudentDto> registerHandler,
        IHandler<GetStudentQuery, StudentDto> getHandler,
        IHandler<DeleteStudentCommand> deleteHandler)
    {
        _registerHandler = registerHandler;
        _getHandler = getHandler;
        _deleteHandler = deleteHandler;
    }

    [HttpGet("{id}", Name = nameof(GetById))]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetStudentQuery(id);
        var result = await _getHandler.Handle(query, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterStudentRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterStudentCommand(request.Name, request.Email);
        var result = await _registerHandler.Handle(command, cancellationToken);
        return result.ToActionResult(this, nameof(GetById), dto => new { id = dto.Id });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteStudentCommand(id);
        var result = await _deleteHandler.Handle(command, cancellationToken);
        return result.ToActionResult(this);
    }
}
```

One-liner result handling — no repetitive if/else in every action.

---

## 🛡️ Global Exception Middleware

```csharp
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            // Value object validation failures
            _logger.LogWarning(ex, "Validation error for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteResponseAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            // Unexpected errors
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteResponseAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred");
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
```

Register in `Program.cs`:

```csharp
app.UseMiddleware<ExceptionMiddleware>();
```

---

## 🔁 Idempotency Middleware

Idempotency ensures that duplicate requests (e.g., retries after timeout) produce the same response — including the **same status code**. This prevents duplicate orders, payments, or any state mutation from network retries.

### Interface

```csharp
// Application/Abstractions/IIdempotencyService.cs
public interface IIdempotencyService
{
    Task<IdempotencyResult> TryGetAsync(string key, CancellationToken ct = default);
    Task StoreAsync(string key, int statusCode, string body, TimeSpan? expiry = null, CancellationToken ct = default);
}

public record IdempotencyResult(bool Exists, int StatusCode, string? Body);
```

### Middleware

```csharp
// API/Middleware/IdempotencyMiddleware.cs
public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private const string IdempotencyKeyHeader = "X-Idempotency-Key";

    private static readonly HashSet<string> IdempotentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyService idempotencyService)
    {
        // Skip safe methods (GET, HEAD, OPTIONS)
        if (!IdempotentMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // No idempotency key provided - proceed normally
        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = keyValues.First()!;
        var cached = await idempotencyService.TryGetAsync(idempotencyKey, context.RequestAborted);

        if (cached.Exists)
        {
            // Return EXACT same response (status + body)
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Idempotent-Replayed"] = "true";

            if (!string.IsNullOrEmpty(cached.Body))
                await context.Response.WriteAsync(cached.Body, context.RequestAborted);

            return;
        }

        // Capture and store response
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            memoryStream.Position = 0;
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync(context.RequestAborted);

            await idempotencyService.StoreAsync(
                idempotencyKey,
                context.Response.StatusCode,
                responseBody,
                ct: context.RequestAborted);

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
```

### In-Memory Implementation (Development)

```csharp
// Infrastructure/Services/IdempotencyService.cs
public sealed class IdempotencyService : IIdempotencyService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    public IdempotencyService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<IdempotencyResult> TryGetAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = $"idempotency:{key}";
        if (_cache.TryGetValue(cacheKey, out CachedResponse? cached) && cached is not null)
            return Task.FromResult(new IdempotencyResult(true, cached.StatusCode, cached.Body));

        return Task.FromResult(new IdempotencyResult(false, 0, null));
    }

    public Task StoreAsync(string key, int statusCode, string body, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var cacheKey = $"idempotency:{key}";
        _cache.Set(cacheKey, new CachedResponse(statusCode, body), expiry ?? DefaultExpiry);
        return Task.CompletedTask;
    }

    private sealed record CachedResponse(int StatusCode, string Body);
}
```

### Redis Implementation (Production)

```csharp
// Infrastructure/Services/RedisIdempotencyService.cs
public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    public RedisIdempotencyService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<IdempotencyResult> TryGetAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync($"idempotency:{key}");

        if (value.IsNullOrEmpty)
            return new IdempotencyResult(false, 0, null);

        var cached = JsonSerializer.Deserialize<CachedResponse>((string)value!);
        return new IdempotencyResult(true, cached!.StatusCode, cached.Body);
    }

    public async Task StoreAsync(string key, int statusCode, string body, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var serialized = JsonSerializer.Serialize(new CachedResponse(statusCode, body));
        await db.StringSetAsync($"idempotency:{key}", serialized, expiry ?? DefaultExpiry);
    }

    private sealed record CachedResponse(int StatusCode, string Body);
}
```

### Configuration-Based Registration

```csharp
// Infrastructure/DependencyInjection.cs

// Idempotency (Redis for prod, in-memory for dev)
var redisConnection = config.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConnection));
    services.AddSingleton<IIdempotencyService, RedisIdempotencyService>();
}
else
{
    services.AddSingleton<IIdempotencyService, IdempotencyService>();
}
```

### Usage

```bash
# First request - executes handler, returns 201
curl -X POST /api/students \
  -H "X-Idempotency-Key: order-abc-123" \
  -d '{"name": "John", "email": "john@example.com"}'
# Response: 201 Created

# Retry (same key) - returns cached 201, not 409
curl -X POST /api/students \
  -H "X-Idempotency-Key: order-abc-123" \
  -d '{"name": "John", "email": "john@example.com"}'
# Response: 201 Created + X-Idempotent-Replayed: true
```

### Idempotency Behavior

| Request  | Header                   | Response     | Note                               |
| -------- | ------------------------ | ------------ | ---------------------------------- |
| 1st POST | `X-Idempotency-Key: abc` | 201 + body   | Executed, cached                   |
| 2nd POST | `X-Idempotency-Key: abc` | 201 + body   | Replayed from cache                |
| 3rd POST | No header                | 409 Conflict | No idempotency, duplicate detected |
| 4th POST | `X-Idempotency-Key: xyz` | 409 Conflict | Different key, duplicate detected  |

> **Key insight**: True idempotency returns **identical responses** including status codes. Returning 409 on retry breaks client retry logic.

---

## 📝 Structured Logging (Serilog)

Structured logging captures log data as queryable properties, not just text. Combined with correlation IDs, it enables request tracing across services.

### Program.cs Configuration

```csharp
using Serilog;
using Serilog.Events;

// Bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting application");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "CleanKissApi")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

    // ... rest of builder configuration

    var app = builder.Build();

    // Correlation ID (must be first to enrich all logs)
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        };
    });

    // ... rest of middleware

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

### Correlation ID Middleware

```csharp
// API/Middleware/CorrelationIdMiddleware.cs
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or generate correlation ID
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // Store in HttpContext for access throughout the request
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers for client tracking
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Add to Serilog LogContext for structured logging
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
```

### Log Output Examples

**Console (human-readable):**

```
[14:32:15 INF] HTTP GET /api/students/123 responded 200 in 45.2ms {"CorrelationId":"abc123","RequestHost":"localhost:5000"}
[14:32:16 WRN] Validation error: Email is invalid | Method: POST | Path: /api/students {"CorrelationId":"def456"}
```

**File (machine-parseable):**

```
2026-02-03 14:32:15.123 +00:00 [INF] abc123 HTTP GET /api/students/123 responded 200 in 45.2ms
2026-02-03 14:32:16.456 +00:00 [WRN] def456 Validation error: Email is invalid | Method: POST | Path: /api/students
```

### appsettings.json Configuration

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning"
      }
    }
  }
}
```

### Production Sinks

For production, add sinks for centralized logging:

```csharp
// Seq (self-hosted)
.WriteTo.Seq("http://seq-server:5341")

// Azure Application Insights
.WriteTo.ApplicationInsights(TelemetryConfiguration.Active, TelemetryConverter.Traces)

// Elasticsearch
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://elastic:9200")))
```

### Correlation ID Flow

```
Client Request
  ↓ X-Correlation-Id: abc123 (or generated)
CorrelationIdMiddleware
  ↓ Pushed to LogContext
All Logs Include CorrelationId
  ↓
Response
  ↓ X-Correlation-Id: abc123 (echoed back)
Client
```

| Header             | Direction | Purpose                      |
| ------------------ | --------- | ---------------------------- |
| `X-Correlation-Id` | Request   | Client-provided or generated |
| `X-Correlation-Id` | Response  | Echoed for client tracking   |

> **Tip**: Pass `X-Correlation-Id` between microservices to trace requests across the entire system.

---

## � Health Checks

Health checks are essential for container orchestrators (Kubernetes), load balancers, and monitoring systems to determine if your application is healthy and ready to receive traffic.

### Two Endpoints

| Endpoint        | Purpose                                 | Checks                   |
| --------------- | --------------------------------------- | ------------------------ |
| `/health/live`  | **Liveness** - Is the app running?      | None (just responds 200) |
| `/health/ready` | **Readiness** - Can it handle requests? | Database, Redis, etc.    |

### Configuration in Program.cs

```csharp
// Health checks
var healthChecks = builder.Services.AddHealthChecks();

// SQL Server health check
var sqlConnection = builder.Configuration.GetConnectionString("Default");
if (!string.IsNullOrEmpty(sqlConnection))
{
    healthChecks.AddSqlServer(sqlConnection, name: "sqlserver", tags: ["db", "ready"]);
}

// Redis health check (if configured)
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    healthChecks.AddRedis(redisConnection, name: "redis", tags: ["cache", "ready"]);
}
```

### Endpoint Mapping

```csharp
// Liveness - just confirms app is running
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // No dependency checks
});

// Readiness - checks all dependencies tagged "ready"
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                exception = e.Value.Exception?.Message
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});
```

### Response Examples

**Liveness (GET /health/live):**

```
HTTP 200 OK
Healthy
```

**Readiness (GET /health/ready):**

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "sqlserver",
      "status": "Healthy",
      "duration": 12.5,
      "exception": null
    },
    {
      "name": "redis",
      "status": "Healthy",
      "duration": 3.2,
      "exception": null
    }
  ],
  "totalDuration": 15.7
}
```

**Unhealthy Response:**

```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "sqlserver",
      "status": "Unhealthy",
      "duration": 5001.2,
      "exception": "Connection timeout"
    }
  ],
  "totalDuration": 5001.2
}
```

### Kubernetes Configuration

```yaml
apiVersion: v1
kind: Pod
spec:
  containers:
    - name: api
      livenessProbe:
        httpGet:
          path: /health/live
          port: 80
        initialDelaySeconds: 5
        periodSeconds: 10
      readinessProbe:
        httpGet:
          path: /health/ready
          port: 80
        initialDelaySeconds: 10
        periodSeconds: 5
```

### Health Check Flow

```
Kubernetes/Load Balancer
  ↓
/health/live → App running? → 200 OK (keep pod alive)
  ↓
/health/ready → Dependencies OK? → 200 OK (route traffic)
                                 → 503 (stop routing traffic)
```

| Probe         | Failure Action            | Use Case                   |
| ------------- | ------------------------- | -------------------------- |
| **Liveness**  | Restart container         | App deadlocked/frozen      |
| **Readiness** | Remove from load balancer | DB down, still starting up |

> **Key insight**: Liveness should be cheap (no I/O). Readiness can check dependencies.

---

## 🚦 Rate Limiting

Rate limiting protects your API from abuse, DDoS attacks, and runaway clients. Built into .NET with zero external dependencies.

### Configuration

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global: 100 requests per minute per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // Strict policy: 10 requests per minute (for sensitive endpoints)
    options.AddPolicy("strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please try again later." },
            cancellationToken);
    };
});
```

### Middleware Registration

```csharp
app.UseRateLimiter();
```

### Applying Strict Policy to Endpoints

```csharp
// On a controller
[EnableRateLimiting("strict")]
public class AuthController : ControllerBase { }

// On a specific action
[HttpPost]
[EnableRateLimiting("strict")]
public async Task<IActionResult> Register(...) { }

// Disable rate limiting for specific endpoints
[DisableRateLimiting]
public async Task<IActionResult> HealthCheck() { }
```

### Rate Limit Policies

| Policy   | Limit   | Window | Use Case                            |
| -------- | ------- | ------ | ----------------------------------- |
| Global   | 100 req | 1 min  | All endpoints by default            |
| `strict` | 10 req  | 1 min  | Login, registration, password reset |

### Response on Limit Exceeded

```
HTTP 429 Too Many Requests
Content-Type: application/json

{
  "error": "Too many requests. Please try again later."
}
```

### Algorithm Options

| Algorithm          | Behavior                   | Use Case                     |
| ------------------ | -------------------------- | ---------------------------- |
| **Fixed Window**   | Resets at fixed intervals  | Simple, predictable          |
| **Sliding Window** | Rolling window             | Smoother distribution        |
| **Token Bucket**   | Tokens replenish over time | Allows bursts                |
| **Concurrency**    | Limits concurrent requests | Protect expensive operations |

> **Tip**: Start with Fixed Window. Only switch if you have specific burst or smoothing requirements.

---

## 🔢 API Versioning

API versioning allows you to evolve your API without breaking existing clients. Supports URL segment and header-based versioning.

### Configuration

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
}).AddMvc();
```

### Controller Setup

```csharp
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class StudentsController : ControllerBase
{
    // ...
}
```

### Versioning Strategies

| Strategy    | Example                             | Use Case               |
| ----------- | ----------------------------------- | ---------------------- |
| URL Segment | `GET /api/v1/students`              | Most explicit, RESTful |
| Header      | `X-Api-Version: 1.0`                | Cleaner URLs           |
| Query       | `GET /api/students?api-version=1.0` | Easy for testing       |

### Usage Examples

```bash
# URL segment (preferred)
curl https://api.example.com/api/v1/students

# Header-based
curl https://api.example.com/api/v1/students \
  -H "X-Api-Version: 1.0"

# Response includes version info
# api-supported-versions: 1.0
# api-deprecated-versions: (if any)
```

### Adding a New Version

```csharp
[ApiController]
[ApiVersion(1.0)]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class StudentsController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id) { ... }

    [HttpGet("{id}")]
    [MapToApiVersion(2.0)]
    public async Task<IActionResult> GetByIdV2(Guid id)
    {
        // New response format, additional fields, etc.
    }
}
```

### Deprecating a Version

```csharp
[ApiVersion(1.0, Deprecated = true)]
[ApiVersion(2.0)]
public class StudentsController : ControllerBase { }
```

Response headers will include:

```
api-deprecated-versions: 1.0
api-supported-versions: 2.0
```

### Version Negotiation

| Request                     | Version Used | Notes                      |
| --------------------------- | ------------ | -------------------------- |
| `/api/v1/students`          | 1.0          | URL segment takes priority |
| `/api/v2/students`          | 2.0          | Explicit version           |
| `/api/students` + header    | From header  | Falls back to header       |
| `/api/students` (no header) | 1.0          | Default when unspecified   |

> **Tip**: Use URL segment versioning as primary — it's explicit and works with all HTTP clients including browsers.

---

## 📡 OpenTelemetry (Distributed Tracing)

OpenTelemetry provides vendor-neutral distributed tracing, enabling you to track requests across services and identify performance bottlenecks.

### Configuration

```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "CleanKissApi",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddConsoleExporter());
```

### Instrumentation Sources

| Instrumentation | Traces                 | Use Case               |
| --------------- | ---------------------- | ---------------------- |
| ASP.NET Core    | Incoming HTTP requests | API endpoint latency   |
| HttpClient      | Outgoing HTTP calls    | External service calls |
| SqlClient       | Database queries       | Query performance      |
| Custom          | Your business logic    | Handler execution time |

### Production Exporters

Replace `AddConsoleExporter()` with production backends:

```csharp
// Jaeger
.AddJaegerExporter(options =>
{
    options.AgentHost = "jaeger";
    options.AgentPort = 6831;
})

// OTLP (OpenTelemetry Protocol) - works with many backends
.AddOtlpExporter(options =>
{
    options.Endpoint = new Uri("http://otel-collector:4317");
})

// Azure Monitor / Application Insights
.AddAzureMonitorTraceExporter(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
})

// Zipkin
.AddZipkinExporter(options =>
{
    options.Endpoint = new Uri("http://zipkin:9411/api/v2/spans");
})
```

### Custom Spans

Add custom tracing to handlers:

```csharp
using System.Diagnostics;

public class RegisterStudentHandler : IHandler<RegisterStudentCommand, StudentDto>
{
    private static readonly ActivitySource ActivitySource = new("CleanKissApi.Handlers");

    public async Task<Result<StudentDto>> Handle(RegisterStudentCommand command, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("RegisterStudent");
        activity?.SetTag("student.email", command.Email);

        // ... handler logic

        activity?.SetTag("student.id", student.Id.ToString());
        return Result<StudentDto>.Created(student.ToDto());
    }
}

// Register the custom ActivitySource
.WithTracing(tracing => tracing
    .AddSource("CleanKissApi.Handlers")
    // ... other instrumentation
)
```

### Trace Context Propagation

OpenTelemetry automatically propagates trace context via W3C Trace Context headers:

```
traceparent: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01
tracestate: congo=t61rcWkgMzE
```

This enables end-to-end tracing across microservices.

### Trace Output Example

```
Activity.TraceId:          0af7651916cd43dd8448eb211c80319c
Activity.SpanId:           b7ad6b7169203331
Activity.DisplayName:      GET /api/v1/students/123
Activity.Kind:             Server
Activity.StartTime:        2026-02-03T14:32:15.1234567Z
Activity.Duration:         00:00:00.0452341
Activity.Tags:
    http.method: GET
    http.url: https://localhost:5001/api/v1/students/123
    http.status_code: 200
    db.system: mssql
    db.name: Students
```

### Benefits

| Feature                  | Benefit                                |
| ------------------------ | -------------------------------------- |
| **Distributed Tracing**  | Track requests across services         |
| **Performance Analysis** | Identify slow endpoints and queries    |
| **Error Correlation**    | Link errors to specific request traces |
| **Vendor Neutral**       | Switch backends without code changes   |
| **Auto-instrumentation** | HTTP, SQL, gRPC traced automatically   |

> **Tip**: Start with console exporter for development, then switch to Jaeger/OTLP for production visualization.

---

## ⚙️ Configuration Validation

Fail fast at startup if required configuration is missing or invalid. No more runtime surprises from misconfigured apps.

### Settings Classes

```csharp
// Application/Settings/DatabaseSettings.cs
using System.ComponentModel.DataAnnotations;

public class DatabaseSettings
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage = "Database connection string 'Default' is required")]
    public string Default { get; init; } = string.Empty;

    public string? Redis { get; init; }
}
```

```csharp
// Application/Settings/JwtSettings.cs
using System.ComponentModel.DataAnnotations;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "JWT Secret is required")]
    [MinLength(32, ErrorMessage = "JWT Secret must be at least 32 characters")]
    public string Secret { get; init; } = string.Empty;

    [Required(ErrorMessage = "JWT Issuer is required")]
    public string Issuer { get; init; } = string.Empty;

    [Required(ErrorMessage = "JWT Audience is required")]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "JWT ExpirationMinutes must be between 1 and 1440")]
    public int ExpirationMinutes { get; init; } = 60;
}
```

### Registration in Program.cs

```csharp
// Configuration validation (fail fast on missing required settings)
builder.Services.AddOptions<DatabaseSettings>()
    .BindConfiguration(DatabaseSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration(JwtSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### appsettings.json Example

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=CleanKiss;Trusted_Connection=True;",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Secret": "your-super-secret-key-at-least-32-chars",
    "Issuer": "CleanKissApi",
    "Audience": "CleanKissApi",
    "ExpirationMinutes": 60
  }
}
```

### Startup Failure Example

If configuration is missing or invalid, the app fails immediately with a clear error:

```
Unhandled exception. Microsoft.Extensions.Options.OptionsValidationException:
DataAnnotation validation failed for 'JwtSettings' members:
  'Secret' with the error: 'JWT Secret is required'.
  'Issuer' with the error: 'JWT Issuer is required'.
```

### Injecting Validated Settings

```csharp
public class AuthService
{
    private readonly JwtSettings _settings;

    public AuthService(IOptions<JwtSettings> options)
    {
        _settings = options.Value; // Guaranteed valid at this point
    }

    public string GenerateToken(...)
    {
        // Use _settings.Secret, _settings.Issuer, etc.
    }
}
```

### Validation Attributes

| Attribute             | Purpose               | Example                         |
| --------------------- | --------------------- | ------------------------------- |
| `[Required]`          | Must have a value     | Connection strings, secrets     |
| `[MinLength]`         | Minimum string length | Passwords, API keys             |
| `[MaxLength]`         | Maximum string length | Names, descriptions             |
| `[Range]`             | Numeric range         | Ports, timeouts, retry counts   |
| `[Url]`               | Valid URL format      | API endpoints, webhook URLs     |
| `[EmailAddress]`      | Valid email format    | Admin email, notification email |
| `[RegularExpression]` | Custom pattern        | API key format, custom IDs      |

### Custom Validation

For complex rules, implement `IValidatableObject`:

```csharp
public class FeatureSettings : IValidatableObject
{
    public bool EnableFeatureX { get; init; }
    public string? FeatureXApiKey { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (EnableFeatureX && string.IsNullOrEmpty(FeatureXApiKey))
        {
            yield return new ValidationResult(
                "FeatureXApiKey is required when EnableFeatureX is true",
                new[] { nameof(FeatureXApiKey) });
        }
    }
}
```

### Benefits

| Benefit          | Description                                        |
| ---------------- | -------------------------------------------------- |
| **Fail Fast**    | App won't start with invalid config                |
| **Clear Errors** | Specific messages for each missing/invalid setting |
| **Type Safety**  | Strongly-typed settings, no magic strings          |
| **Testable**     | Settings classes can be unit tested                |
| **Discoverable** | All required settings documented in one place      |

> **Key insight**: Catch configuration errors at startup, not at 3 AM in production when a code path finally hits the missing setting.

---

## 🎯 Error Handling Strategy

| Error Type              | Handled By                       | HTTP Status               |
| ----------------------- | -------------------------------- | ------------------------- |
| Business rule failure   | `Result<T>.Failure()`            | 400/404/409 (as defined)  |
| Value object validation | `ArgumentException` → Middleware | 400 Bad Request           |
| Unexpected errors       | `Exception` → Middleware         | 500 Internal Server Error |

- **Expected failures** → Use `Result<T>` (no exceptions)
- **Invalid value objects** → Caught by middleware (fail-fast)
- **Unexpected errors** → Logged and return generic message (no stack trace leak)

---

## 🔌 Dependency Injection Setup

### Project References

```
API → Application → Domain
API → Infrastructure → Domain
```

- **Domain** has no dependencies (pure)
- **Application** depends on Domain (uses interfaces, entities, value objects)
- **Infrastructure** depends on Domain (implements interfaces)
- **API** depends on Application + Infrastructure (wires everything together)

### Infrastructure Registration

```csharp
// Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // DbContext
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("Default")));

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();

        // External services
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IClock, Clock>();

        // Caching (choose one)
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, CacheService>();

        // Or for Redis:
        // services.AddStackExchangeRedisCache(options =>
        //     options.Configuration = config.GetConnectionString("Redis"));
        // services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
```

### Application Registration

```csharp
// Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Handlers with return value
        services.AddScoped<IHandler<RegisterStudentCommand, StudentDto>, RegisterStudentHandler>();
        services.AddScoped<IHandler<GetStudentQuery, StudentDto>, GetStudentHandler>();

        // Handlers without return value
        services.AddScoped<IHandler<DeleteStudentCommand>, DeleteStudentHandler>();

        return services;
    }
}
```

### API Composition Root (Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register layers
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddControllers();

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionMiddleware>();
app.MapControllers();

app.Run();
```

Clean, explicit wiring — no assembly scanning magic.

---

## � Testing Strategy

### Test Project Structure

| Project                | Tests                                 | Dependencies          |
| ---------------------- | ------------------------------------- | --------------------- |
| `Domain.Tests`         | Value objects, entities, domain logic | None (pure)           |
| `Application.Tests`    | Handlers, business rules              | Mocked repositories   |
| `Infrastructure.Tests` | Repositories, EF queries              | In-memory database    |
| `API.Tests`            | Controllers, integration              | WebApplicationFactory |

### Domain Tests (Pure, No Mocks)

```csharp
public class EmailTests
{
    [Fact]
    public void Constructor_WithValidEmail_CreatesEmail()
    {
        // Arrange & Act
        var email = new Email("test@example.com");

        // Assert
        Assert.Equal("test@example.com", email.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_WithEmptyEmail_ThrowsArgumentException(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Email(value!));
    }

    [Fact]
    public void Constructor_WithInvalidFormat_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Email("invalid-email"));
    }
}
```

### Handler Tests (Mocked Dependencies)

```csharp
public class RegisterStudentHandlerTests
{
    private readonly Mock<IStudentRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly RegisterStudentHandler _handler;

    public RegisterStudentHandlerTests()
    {
        _repositoryMock = new Mock<IStudentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheMock = new Mock<ICacheService>();
        _handler = new RegisterStudentHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WithNewEmail_ReturnsCreated()
    {
        // Arrange
        var command = new RegisterStudentCommand("John Doe", "john@example.com");
        _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("john@example.com", result.Value!.Email);
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ReturnsConflict()
    {
        // Arrange
        var command = new RegisterStudentCommand("John Doe", "existing@example.com");
        _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ThrowsArgumentException()
    {
        // Arrange
        var command = new RegisterStudentCommand("John Doe", "invalid-email");
        _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command));
    }

    [Fact]
    public async Task Handle_Success_InvalidatesCache()
    {
        // Arrange
        var command = new RegisterStudentCommand("John Doe", "john@example.com");
        _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.Handle(command);

        // Assert
        _cacheMock.Verify(c => c.RemoveAsync(CacheKeys.AllStudents, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Integration Tests (WebApplicationFactory)

```csharp
public class StudentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StudentsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace DbContext with in-memory
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb"));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new { Name = "John Doe", Email = "john@example.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/students", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var request = new { Name = "John Doe", Email = "duplicate@example.com" };
        await _client.PostAsJsonAsync("/api/students", request);

        // Act
        var response = await _client.PostAsJsonAsync("/api/students", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/students/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific project
dotnet test tests/Application.Tests
```

---

## �🧠 Why This Architecture Works

- ✅ Easy to debug locally
- ✅ Easy to test
- ✅ Easy to extend
- ✅ No unnecessary layers
- ✅ No folder explosion
- ✅ No CQRS/DDD theatre
- ✅ Domain stays pure
- ✅ Handlers stay small
- ✅ API stays thin
- ✅ Validation is co-located, not scattered
- ✅ No exception-driven control flow
- ✅ Global exception handling as safety net
- ✅ CancellationToken support throughout
- ✅ Unit of Work for explicit transaction control
- ✅ Simple caching with cache-aside pattern

> This is the architecture you build when you care about clarity, maintainability, and real-world productivity.
