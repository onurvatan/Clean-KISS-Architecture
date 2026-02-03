Clean KISS Architecture
A simple, maintainable, domain‑first architecture designed around clarity, testability, and real‑world development needs.
No ceremony. No over‑engineering. No “enterprise theatre.”
Just clean boundaries, predictable behaviour, and code you can debug locally.

📁 Folder Structure
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
RegisterStudent
RegisterStudentCommand.cs
RegisterStudentHandler.cs
GetStudent
GetStudentQuery.cs
GetStudentHandler.cs
/Services
IEmailService.cs
IClock.cs
/Abstractions
IHandler.cs // generic handler interface for TDD

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
/Models
RegisterStudentRequest.cs
StudentResponse.cs

🎯 Architectural Principles

1. Domain is pure

- No EF Core attributes
- No DTOs
- No API concerns
- Only business rules, invariants, and value objects

2. Application orchestrates

- Handlers implement IHandler<TRequest, TResponse>
- Business validation lives here
- Mapping done via extension methods
- DTOs defined here

3. Infrastructure handles persistence

- EF Core DbContext
- Repository implementations
- External services (email, clock, etc.)

4. API is thin

- Controllers only accept requests
- Call handlers
- Return responses
- No business logic

🧩 Generic Handler Interface (for TDD)
public interface IHandler<TRequest, TResponse>
{
Task<TResponse> Handle(TRequest request);
}

Simple, generic, and perfect for testing.

🧪 Example Value Object
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

🔄 Mapping via Extensions
public static class StudentExtensions
{
public static StudentDto ToDto(this Student s)
=> new StudentDto(s.Id, s.Name.Value, s.Email.Value);
}

No AutoMapper.
No magic.
Just clean, explicit mapping.

🧠 Why This Architecture Works

- Easy to debug locally
- Easy to test
- Easy to extend
- No unnecessary layers
- No folder explosion
- No CQRS/DDD theatre
- Domain stays pure
- Handlers stay small
- API stays thin
  This is the architecture you build when you care about clarity, maintainability, and real‑world productivity.
