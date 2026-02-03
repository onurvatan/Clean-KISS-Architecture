using Domain.ValueObjects;

namespace Domain.Entities;

public class Student
{
    public Guid Id { get; private set; }
    public Name Name { get; private set; }
    public Email Email { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // EF Core constructor
    private Student()
    {
        Name = null!;
        Email = null!;
    }

    public Student(Name name, Email email)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateName(Name name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateEmail(Email email)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        UpdatedAt = DateTime.UtcNow;
    }
}
