using Application.Abstractions;
using Application.DTOs;
using Application.Extensions;
using Application.Handlers.RegisterStudent;
using Application.Services;
using Domain.Interfaces;
using Moq;

namespace Application.Tests.Handlers;

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
        Assert.Equal("John Doe", result.Value.Name);
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
        Assert.Contains("already exists", result.Error);
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
    public async Task Handle_WithInvalidName_ThrowsArgumentException()
    {
        // Arrange
        var command = new RegisterStudentCommand("", "john@example.com");
        _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command));
    }

    [Fact]
    public async Task Handle_Success_CallsRepositoryAdd()
    {
        // Arrange
        var command = new RegisterStudentCommand("John Doe", "john@example.com");
        _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.Handle(command);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Student>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Success_CallsUnitOfWorkSave()
    {
        // Arrange
        var command = new RegisterStudentCommand("John Doe", "john@example.com");
        _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.Handle(command);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task Handle_Failure_DoesNotCallUnitOfWork()
    {
        // Arrange
        var command = new RegisterStudentCommand("John Doe", "existing@example.com");
        _repositoryMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(command);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
