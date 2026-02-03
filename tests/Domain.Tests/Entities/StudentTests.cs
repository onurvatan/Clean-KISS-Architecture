using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Tests.Entities;

public class StudentTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesStudent()
    {
        // Arrange
        var name = new Name("John Doe");
        var email = new Email("john@example.com");

        // Act
        var student = new Student(name, email);

        // Assert
        Assert.NotEqual(Guid.Empty, student.Id);
        Assert.Equal("John Doe", student.Name.Value);
        Assert.Equal("john@example.com", student.Email.Value);
        Assert.True(student.CreatedAt <= DateTime.UtcNow);
        Assert.Null(student.UpdatedAt);
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var email = new Email("john@example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Student(null!, email));
    }

    [Fact]
    public void Constructor_WithNullEmail_ThrowsArgumentNullException()
    {
        // Arrange
        var name = new Name("John Doe");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Student(name, null!));
    }

    [Fact]
    public void UpdateName_WithValidName_UpdatesNameAndTimestamp()
    {
        // Arrange
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var newName = new Name("Jane Doe");

        // Act
        student.UpdateName(newName);

        // Assert
        Assert.Equal("Jane Doe", student.Name.Value);
        Assert.NotNull(student.UpdatedAt);
    }

    [Fact]
    public void UpdateName_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => student.UpdateName(null!));
    }

    [Fact]
    public void UpdateEmail_WithValidEmail_UpdatesEmailAndTimestamp()
    {
        // Arrange
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var newEmail = new Email("jane@example.com");

        // Act
        student.UpdateEmail(newEmail);

        // Assert
        Assert.Equal("jane@example.com", student.Email.Value);
        Assert.NotNull(student.UpdatedAt);
    }

    [Fact]
    public void UpdateEmail_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => student.UpdateEmail(null!));
    }
}
