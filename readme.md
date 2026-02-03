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
    /Services
      IEmailService.cs
      IClock.cs
    /Abstractions
      IHandler.cs
      Result.cs

  /Infrastructure
    /Persistence
      /EF
        AppDbContext.cs
        StudentConfiguration.cs
        CourseConfiguration.cs
      /Repositories
        StudentRepository.cs
        CourseRepository.cs
    /Services
      EmailService.cs
      Clock.cs

  /API
    /Controllers
      StudentsController.cs
      CoursesController.cs
    /Middleware
      ExceptionMiddleware.cs
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

```csharp
public class Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    private Result(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}
```

No exceptions for expected failures. Clean, predictable control flow.

---

## 🧩 Generic Handler Interface

```csharp
public interface IHandler<TRequest, TResponse>
{
    Task<Result<TResponse>> Handle(TRequest request);
}
```

Simple, generic, testable, and returns a result instead of throwing.

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

## 🧪 Example Handler (with business validation)

```csharp
public class RegisterStudentHandler : IHandler<RegisterStudentCommand, StudentDto>
{
    private readonly IStudentRepository _repository;

    public RegisterStudentHandler(IStudentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<StudentDto>> Handle(RegisterStudentCommand command)
    {
        // Business validation: check uniqueness
        var exists = await _repository.ExistsByEmailAsync(command.Email);
        if (exists)
            return Result<StudentDto>.Failure("A student with this email already exists");

        // Value object validates format (throws if invalid)
        var email = new Email(command.Email);
        var student = new Student(command.Name, email);

        await _repository.AddAsync(student);

        return Result<StudentDto>.Success(student.ToDto());
    }
}
```

- **Handler** checks business rules (email uniqueness)
- **Value object** validates structure (email format)
- **Result** returns success or failure — no exception-driven flow

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

```csharp
[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IHandler<RegisterStudentCommand, StudentDto> _handler;

    public StudentsController(IHandler<RegisterStudentCommand, StudentDto> handler)
    {
        _handler = handler;
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterStudentRequest request)
    {
        var command = new RegisterStudentCommand(request.Name, request.Email);
        var result = await _handler.Handle(command);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}
```

Clean mapping from `Result<T>` to HTTP status codes.

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
            _logger.LogWarning(ex, "Validation error");
            await WriteResponseAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            // Unexpected errors
            _logger.LogError(ex, "Unhandled exception");
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
| Business rule failure   | `Result<T>.Failure()`            | 400 Bad Request           |
| Value object validation | `ArgumentException` → Middleware | 400 Bad Request           |
| Unexpected errors       | `Exception` → Middleware         | 500 Internal Server Error |

- **Expected failures** → Use `Result<T>` (no exceptions)
- **Invalid value objects** → Caught by middleware (fail-fast)
- **Unexpected errors** → Logged and return generic message (no stack trace leak)

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

> This is the architecture you build when you care about clarity, maintainability, and real-world productivity.
