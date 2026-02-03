using Application.Abstractions;
using Application.DTOs;
using Application.Extensions;
using Application.Handlers.GetStudent;
using Application.Services;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Moq;

namespace Application.Tests.Handlers;

public class GetStudentHandlerTests
{
    private readonly Mock<IStudentRepository> _repositoryMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly GetStudentHandler _handler;

    public GetStudentHandlerTests()
    {
        _repositoryMock = new Mock<IStudentRepository>();
        _cacheMock = new Mock<ICacheService>();
        _handler = new GetStudentHandler(_repositoryMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingStudent_ReturnsSuccess()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var query = new GetStudentQuery(studentId);

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StudentDto?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(student.ToDto());

        // Act
        var result = await _handler.Handle(query);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("john@example.com", result.Value!.Email);
    }

    [Fact]
    public async Task Handle_WithNonExistingStudent_ReturnsNotFound()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var query = new GetStudentQuery(studentId);

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StudentDto?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((StudentDto?)null);

        // Act
        var result = await _handler.Handle(query);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task Handle_UsesCacheKey()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var query = new GetStudentQuery(studentId);
        var expectedCacheKey = CacheKeys.Student(studentId);

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                expectedCacheKey,
                It.IsAny<Func<Task<StudentDto?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((StudentDto?)null);

        // Act
        await _handler.Handle(query);

        // Assert
        _cacheMock.Verify(c => c.GetOrCreateAsync(
            expectedCacheKey,
            It.IsAny<Func<Task<StudentDto?>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CacheMiss_QueriesRepository()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var student = new Student(new Name("John Doe"), new Email("john@example.com"));
        var query = new GetStudentQuery(studentId);

        _repositoryMock.Setup(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Setup cache to call the factory
        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StudentDto?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string key, Func<Task<StudentDto?>> factory, TimeSpan? expiry, CancellationToken ct) =>
                await factory());

        // Act
        var result = await _handler.Handle(query);

        // Assert
        _repositoryMock.Verify(r => r.GetByIdAsync(studentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CancellationRequested_ThrowsOperationCanceled()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var query = new GetStudentQuery(studentId);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<StudentDto?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _handler.Handle(query, cts.Token));
    }
}
