using Domain.ValueObjects;

namespace Domain.Tests.ValueObjects;

public class EmailTests
{
    [Fact]
    public void Constructor_WithValidEmail_CreatesEmail()
    {
        // Arrange & Act
        var email = new Email("test@example.com");

        // Assert
        Assert.Equal("test@example.com", email.Value);
    }

    [Fact]
    public void Constructor_WithUppercaseEmail_NormalizesToLowercase()
    {
        // Arrange & Act
        var email = new Email("Test@EXAMPLE.COM");

        // Assert
        Assert.Equal("test@example.com", email.Value);
    }

    [Fact]
    public void Constructor_WithWhitespace_TrimsEmail()
    {
        // Arrange & Act
        var email = new Email("  test@example.com  ");

        // Assert
        Assert.Equal("test@example.com", email.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespace_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Email(value));
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Email(null!));
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("nodomainemail")]
    [InlineData("missing.at.symbol")]
    public void Constructor_WithInvalidFormat_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Email(value));
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var email1 = new Email("test@example.com");
        var email2 = new Email("test@example.com");

        // Act & Assert
        Assert.Equal(email1, email2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var email1 = new Email("test1@example.com");
        var email2 = new Email("test2@example.com");

        // Act & Assert
        Assert.NotEqual(email1, email2);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var email = new Email("test@example.com");

        // Act & Assert
        Assert.Equal("test@example.com", email.ToString());
    }
}
