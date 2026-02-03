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

  /API
    /Controllers
      StudentsController.cs
      CoursesController.cs
    /Middleware
      ExceptionMiddleware.cs
    /Extensions
      ResultExtensions.cs
    /Models
      RegisterStudentRequest.cs
      StudentResponse.cs
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

## 🔄 Unit of Work

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
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
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

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out T? value) && value is not null)
            return value;

        value = await factory();
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

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory();
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

| Operation | Cache Action | Why |
|-----------|--------------|-----|
| **Query (Get)** | Cache result | Reduce database hits |
| **Create** | Invalidate list cache | List is now stale |
| **Update** | Invalidate item + list cache | Both are stale |
| **Delete** | Invalidate item + list cache | Both are stale |

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
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode switch
            {
                201 => controller.CreatedAtAction(null, result.Value),
                204 => controller.NoContent(),
                _ => controller.Ok(result.Value)
            };
        }

        return result.StatusCode switch
        {
            400 => controller.BadRequest(new { error = result.Error }),
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
    private readonly IHandler<DeleteStudentCommand> _deleteHandler;

    public StudentsController(
        IHandler<RegisterStudentCommand, StudentDto> registerHandler,
        IHandler<DeleteStudentCommand> deleteHandler)
    {
        _registerHandler = registerHandler;
        _deleteHandler = deleteHandler;
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterStudentRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterStudentCommand(request.Name, request.Email);
        var result = await _registerHandler.Handle(command, cancellationToken);
        return result.ToActionResult(this);
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

## 🧠 Why This Architecture Works

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
