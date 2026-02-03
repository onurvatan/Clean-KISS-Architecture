using Application.Abstractions;
using Application.Extensions;
using Application.Handlers.DeleteStudent;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Moq;

namespace Application.Tests.Handlers;

public class DeleteStudentHandlerTests
{
    private readonly Mock<IStudentRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly DeleteStudentHandler _handler;

    public DeleteStudentHandlerTests()
    {
        _repositoryMock = new Mock<IStudentRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheMock = new Mock<ICacheService>();
        _handler = new DeleteStudentHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingStudent_ReturnsNoContent()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var command = new DeleteStudentCommand(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(204, result.StatusCode);
    }

    [Fact]
    public async Task Handle_WithNonExistingStudent_ReturnsNotFound()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var command = new DeleteStudentCommand(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task Handle_Success_CallsRepositoryDelete()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var command = new DeleteStudentCommand(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        await _handler.Handle(command);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(student, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_CallsUnitOfWorkSave()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var command = new DeleteStudentCommand(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        await _handler.Handle(command);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_InvalidatesStudentCache()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var command = new DeleteStudentCommand(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        await _handler.Handle(command);

        // Assert
        _cacheMock.Verify(c => c.RemoveAsync(CacheKeys.Student(studentId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_InvalidatesAllStudentsCache()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var command = new DeleteStudentCommand(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Act
        await _handler.Handle(command);

        // Assert
        _cacheMock.Verify(c => c.RemoveAsync(CacheKeys.AllStudents, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Failure_DoesNotCallDelete()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var command = new DeleteStudentCommand(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        // Act
        await _handler.Handle(command);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Failure_DoesNotCallUnitOfWork()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var command = new DeleteStudentCommand(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        // Act
        await _handler.Handle(command);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
