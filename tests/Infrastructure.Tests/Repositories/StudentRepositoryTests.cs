using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence.EF;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Repositories;

public class StudentRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly StudentRepository _repository;

    public StudentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _repository = new StudentRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingStudent_ReturnsStudent()
    {
        // Arrange
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(student.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(student.Id, result.Id);
        Assert.Equal("john@example.com", result.Email.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingStudent_ReturnsNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistingId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_ValidStudent_AddsToDatabase()
    {
        // Arrange
        var student = new Student(new Name("Jane Doe"), new Email("jane@example.com"));

        // Act
        await _repository.AddAsync(student);
        await _context.SaveChangesAsync();

        // Assert
        var savedStudent = await _context.Students.FindAsync(student.Id);
        Assert.NotNull(savedStudent);
        Assert.Equal("jane@example.com", savedStudent.Email.Value);
    }

    [Fact]
    public async Task ExistsByEmailAsync_ExistingEmail_ReturnsTrue()
    {
        // Arrange
        var student = new Student(new Name("John Doe"), new Email("existing@example.com"));
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsByEmailAsync("existing@example.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByEmailAsync_NonExistingEmail_ReturnsFalse()
    {
        // Arrange & Act
        var result = await _repository.ExistsByEmailAsync("nonexisting@example.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsByEmailAsync_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsByEmailAsync("JOHN@EXAMPLE.COM");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAsync_ExistingStudent_RemovesFromDatabase()
    {
        // Arrange
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(student);
        await _context.SaveChangesAsync();

        // Assert
        var deletedStudent = await _context.Students.FindAsync(student.Id);
        Assert.Null(deletedStudent);
    }

    [Fact]
    public async Task GetAllAsync_MultipleStudents_ReturnsAll()
    {
        // Arrange
        var student1 = new Student(new Name("John Doe"), new Email("john@example.com"));
        var student2 = new Student(new Name("Jane Doe"), new Email("jane@example.com"));
        await _context.Students.AddRangeAsync(student1, student2);
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmpty()
    {
        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetByIdAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _repository.GetByIdAsync(Guid.NewGuid(), cts.Token));
    }
}
