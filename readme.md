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
    /ValueObjects
      Email.cs
      Name.cs
    /Interfaces
      IStudentRepository.cs

  /Application
    /Abstractions
      IHandler.cs
      Result.cs
      IUnitOfWork.cs
      IAuthorizationService.cs
      AuthorizedHandler.cs
      IIdempotencyService.cs
    /DTOs
      StudentDto.cs
    /Extensions
      StudentExtensions.cs
      CacheKeys.cs
    /Handlers
      RegisterStudentHandler.cs
      GetStudentHandler.cs
      DeleteStudentHandler.cs
    /Services
      ICacheService.cs
    /Settings
      DatabaseSettings.cs
      JwtSettings.cs

  /Infrastructure
    /Persistence
      AppDbContext.cs
      StudentRepository.cs
      UnitOfWork.cs
    /Services
      CacheService.cs
      AuthorizationService.cs
      IdempotencyService.cs
      RedisIdempotencyService.cs

  /API
    /Controllers
      StudentsController.cs
    /Middleware
      ExceptionMiddleware.cs
      IdempotencyMiddleware.cs
      CorrelationIdMiddleware.cs
    /Extensions
      ResultExtensions.cs
    Program.cs

/tests
  /Domain.Tests
    EmailTests.cs
    NameTests.cs

  /Application.Tests
    RegisterStudentHandlerTests.cs
    GetStudentHandlerTests.cs

  /Infrastructure.Tests
    StudentRepositoryTests.cs

  /API.Tests
    StudentsControllerTests.cs
```

---

## 🎯 Architectural Principles

### 1. Domain is pure

- No EF Core attributes
- No DTOs
- No API concerns
- Only business rules, invariants, and value objects
- **Value objects validate their own invariants**

### 2. Application orchestrates

- Handlers implement `IHandler<TRequest, TResponse>`
- **Business validation lives in handlers**
- Returns `Result<T>` instead of throwing exceptions
- Mapping via extension methods

### 3. Infrastructure handles I/O

- EF Core DbContext
- Repository implementations
- External services (cache, auth, idempotency)

### 4. API is thin

- Controllers call handlers
- Map `Result<T>` to HTTP responses
- Middleware handles cross-cutting concerns
- No business logic

---

## 🎯 Validation Philosophy

| Layer             | Validates             | Examples                                                |
| ----------------- | --------------------- | ------------------------------------------------------- |
| **Value Objects** | Structural invariants | Email format, Money non-negative, DateRange start < end |
| **Handlers**      | Business rules        | Email uniqueness, authorization, state checks           |

Validation is **co-located with the data it protects** — no scattered logic.

---

## 📦 Result Pattern

```csharp
public class Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public int StatusCode { get; }
    public bool IsSuccess => Error is null;

    private Result(T? value, string? error, int statusCode)
    {
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    // Success
    public static Result<T> Success(T value) => new(value, null, 200);
    public static Result<T> Created(T value) => new(value, null, 201);

    // Failures
    public static Result<T> BadRequest(string error) => new(default, error, 400);
    public static Result<T> Unauthorized(string error) => new(default, error, 401);
    public static Result<T> Forbidden(string error) => new(default, error, 403);
    public static Result<T> NotFound(string error) => new(default, error, 404);
    public static Result<T> Conflict(string error) => new(default, error, 409);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
```

```csharp
public class Result
{
    public string? Error { get; }
    public int StatusCode { get; }
    public bool IsSuccess => Error is null;

    private Result(string? error, int statusCode) { Error = error; StatusCode = statusCode; }

    public static Result Success() => new(null, 200);
    public static Result NoContent() => new(null, 204);
    public static Result BadRequest(string error) => new(error, 400);
    public static Result NotFound(string error) => new(error, 404);
    public static Result Forbidden(string error) => new(error, 403);
}
```

No exceptions for expected failures. Clean, predictable control flow with built-in HTTP semantics.

---

## 🧩 Handler Interface

```csharp
public interface IHandler<TRequest, TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request, CancellationToken ct = default);
}

public interface IHandler<TRequest>
{
    Task<Result> Handle(TRequest request, CancellationToken ct = default);
}
```

---

## 🧪 Value Object Example

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
}
```

If it exists, it's valid.

---

## 🧪 Handler Example

```csharp
public class RegisterStudentHandler : IHandler<RegisterStudentCommand, StudentDto>
{
    private readonly IStudentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<Result<StudentDto>> Handle(RegisterStudentCommand command, CancellationToken ct)
    {
        // Business validation
        if (await _repository.ExistsByEmailAsync(command.Email, ct))
            return Result<StudentDto>.Conflict("Email already exists");

        // Value object validates format
        var email = new Email(command.Email);
        var student = new Student(command.Name, email);

        await _repository.AddAsync(student, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<StudentDto>.Created(student.ToDto());
    }
}
```

---

## 🌐 Controller Example

```csharp
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IHandler<RegisterStudentCommand, StudentDto> _registerHandler;
    private readonly IHandler<GetStudentQuery, StudentDto> _getHandler;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _getHandler.Handle(new GetStudentQuery(id), ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterStudentRequest request, CancellationToken ct)
    {
        var result = await _registerHandler.Handle(
            new RegisterStudentCommand(request.Name, request.Email), ct);
        return result.ToActionResult(this, nameof(GetById), dto => new { id = dto.Id });
    }
}
```

One-liner result handling via `ToActionResult()` extension.

---

## 🔐 Authorization Layer

Authorization is checked **before** handlers execute using a decorator pattern.

### Permissions

```csharp
public static class Permissions
{
    public static class Students
    {
        public const string View = "students:view";
        public const string Create = "students:create";
        public const string Delete = "students:delete";
    }
}
```

### Authorize Attribute

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class AuthorizeAttribute : Attribute
{
    public string? Permission { get; }
    public string? Role { get; }

    public AuthorizeAttribute(string permission) => Permission = permission;
    public static AuthorizeAttribute ForRole(string role) => new() { Role = role };
}
```

### Usage

```csharp
[Authorize(Permissions.Students.Create)]
public class RegisterStudentHandler : IHandler<RegisterStudentCommand, StudentDto>
{
    // Handler implementation
}
```

### Authorization Flow

```
Request → Controller → AuthorizedHandler → [Permission Check] → Handler → Result
                                ↓
                         Forbidden (403) if unauthorized
```

---

## 🔁 Idempotency Middleware

Prevents duplicate operations from network retries by caching responses.

```bash
# First request
curl -X POST /api/v1/students \
  -H "X-Idempotency-Key: order-123" \
  -d '{"name": "John", "email": "john@example.com"}'
# → 201 Created

# Retry (same key) - returns cached response
curl -X POST /api/v1/students \
  -H "X-Idempotency-Key: order-123" \
  -d '{"name": "John", "email": "john@example.com"}'
# → 201 Created + X-Idempotent-Replayed: true
```

| Request  | Header                   | Response            |
| -------- | ------------------------ | ------------------- |
| 1st POST | `X-Idempotency-Key: abc` | 201 + body          |
| 2nd POST | `X-Idempotency-Key: abc` | 201 + body (cached) |

Supports in-memory (dev) and Redis (prod) backends.

---

## 📝 Structured Logging (Serilog)

### Configuration

```csharp
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "CleanKissApi")
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day));
```

### Correlation ID Middleware

Every request gets a correlation ID for tracing:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N");

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        await _next(context);
    }
}
```

### Output

```
[14:32:15 INF] HTTP GET /api/v1/students/123 responded 200 in 45ms {"CorrelationId":"abc123"}
```

---

## 🏥 Health Checks

```csharp
// Registration
var healthChecks = builder.Services.AddHealthChecks();
healthChecks.AddSqlServer(connectionString, name: "sqlserver", tags: ["ready"]);
healthChecks.AddRedis(redisConnection, name: "redis", tags: ["ready"]);

// Endpoints
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
```

| Endpoint        | Purpose             | Checks             |
| --------------- | ------------------- | ------------------ |
| `/health/live`  | Is app running?     | None (just 200 OK) |
| `/health/ready` | Can handle traffic? | DB, Redis, etc.    |

---

## 🚦 Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // Global: 100 requests/minute per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new() { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) }));

    // Strict policy: 10 requests/minute
    options.AddPolicy("strict", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
```

```csharp
[EnableRateLimiting("strict")]
public class AuthController : ControllerBase { }
```

---

## 🔢 API Versioning

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

```csharp
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class StudentsController : ControllerBase { }
```

| Strategy    | Example                |
| ----------- | ---------------------- |
| URL Segment | `GET /api/v1/students` |
| Header      | `X-Api-Version: 1.0`   |

---

## 📡 OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("CleanKissApi"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddConsoleExporter());  // Or Jaeger, OTLP, Zipkin
```

Traces HTTP requests, outgoing calls, and database queries automatically.

---

## ⚙️ Configuration Validation

Fail fast at startup if required settings are missing.

```csharp
public class DatabaseSettings
{
    public const string SectionName = "ConnectionStrings";

    [Required(ErrorMessage = "Connection string 'Default' is required")]
    public string Default { get; init; } = string.Empty;
}

public class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpirationMinutes { get; init; } = 60;
}
```

```csharp
// Program.cs
builder.Services.AddOptions<DatabaseSettings>()
    .BindConfiguration(DatabaseSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// JWT validation in production only
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddOptions<JwtSettings>()
        .BindConfiguration(JwtSettings.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();
}
```

---

## 📦 Caching

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? expiry = null, CancellationToken ct = default);
}
```

```csharp
// Query handler with caching
public async Task<Result<StudentDto>> Handle(GetStudentQuery query, CancellationToken ct)
{
    var dto = await _cache.GetOrCreateAsync(
        CacheKeys.Student(query.Id),
        async () => (await _repository.GetByIdAsync(query.Id, ct))?.ToDto(),
        TimeSpan.FromMinutes(10), ct);

    return dto is null
        ? Result<StudentDto>.NotFound("Student not found")
        : Result<StudentDto>.Success(dto);
}
```

| Operation | Cache Action           |
| --------- | ---------------------- |
| Query     | Cache result           |
| Create    | Invalidate list cache  |
| Update    | Invalidate item + list |
| Delete    | Invalidate item + list |

---

## 🔄 Unit of Work

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    public Task<int> SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
```

Handlers call `SaveChangesAsync()` explicitly — no magic auto-save.

---

## 🛡️ Exception Middleware

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);
    }
    catch (ArgumentException ex)
    {
        // Value object validation failures → 400
        await WriteError(context, 400, ex.Message);
    }
    catch (Exception ex)
    {
        // Unexpected errors → 500 (logged, no stack trace leaked)
        _logger.LogError(ex, "Unhandled exception");
        await WriteError(context, 500, "An unexpected error occurred");
    }
}
```

---

## 🔌 Dependency Injection

```csharp
// Infrastructure/DependencyInjection.cs
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    services.AddDbContext<AppDbContext>(o => o.UseSqlServer(config.GetConnectionString("Default")));
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddScoped<IStudentRepository, StudentRepository>();
    services.AddMemoryCache();
    services.AddSingleton<ICacheService, CacheService>();
    services.AddScoped<IAuthorizationService, AuthorizationService>();
    return services;
}

// Application/DependencyInjection.cs
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddScoped<RegisterStudentHandler>();
    services.AddScoped<IHandler<RegisterStudentCommand, StudentDto>>(sp =>
        new AuthorizedHandler<RegisterStudentCommand, StudentDto>(
            sp.GetRequiredService<RegisterStudentHandler>(),
            sp.GetRequiredService<IAuthorizationService>()));
    return services;
}
```

```csharp
// Program.cs
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
```

---

## 🧪 Testing

| Project                | Tests                    | Dependencies          |
| ---------------------- | ------------------------ | --------------------- |
| `Domain.Tests`         | Value objects, entities  | None (pure)           |
| `Application.Tests`    | Handlers, business rules | Mocked repositories   |
| `Infrastructure.Tests` | Repositories, EF queries | In-memory database    |
| `API.Tests`            | Controllers, integration | WebApplicationFactory |

### Domain Test

```csharp
[Fact]
public void Email_WithInvalidFormat_ThrowsArgumentException()
{
    Assert.Throws<ArgumentException>(() => new Email("invalid"));
}
```

### Handler Test

```csharp
[Fact]
public async Task Handle_WithExistingEmail_ReturnsConflict()
{
    _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var result = await _handler.Handle(new("John", "exists@example.com"));

    Assert.True(result.IsFailure);
    Assert.Equal(409, result.StatusCode);
}
```

```bash
dotnet test
# Test summary: total: 63, failed: 0, succeeded: 63
```

---

## 🧠 Why This Works

- ✅ Easy to debug locally
- ✅ Easy to test
- ✅ Easy to extend
- ✅ No unnecessary layers
- ✅ No CQRS/DDD theatre
- ✅ Domain stays pure
- ✅ Handlers stay small
- ✅ API stays thin
- ✅ Validation is co-located
- ✅ No exception-driven control flow
- ✅ Production-ready features included

---

## 🚀 Production Checklist

| Feature                  | Status |
| ------------------------ | ------ |
| Result Pattern           | ✅     |
| Authorization Layer      | ✅     |
| Idempotency              | ✅     |
| Structured Logging       | ✅     |
| Correlation IDs          | ✅     |
| Health Checks            | ✅     |
| Rate Limiting            | ✅     |
| API Versioning           | ✅     |
| OpenTelemetry            | ✅     |
| Config Validation        | ✅     |
| Caching                  | ✅     |
| Unit of Work             | ✅     |
| Global Exception Handler | ✅     |

> This is the architecture you build when you care about clarity, maintainability, and real-world productivity.
