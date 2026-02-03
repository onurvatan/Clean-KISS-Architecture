using Domain.ValueObjects;

namespace Domain.Tests.ValueObjects;

public class NameTests
{
    [Fact]
    public void Constructor_WithValidName_CreatesName()
    {
        // Arrange & Act
        var name = new Name("John Doe");

        // Assert
        Assert.Equal("John Doe", name.Value);
    }

    [Fact]
    public void Constructor_WithWhitespace_TrimsName()
    {
        // Arrange & Act
        var name = new Name("  John Doe  ");

        // Assert
        Assert.Equal("John Doe", name.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespace_ThrowsArgumentException(string value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Name(value));
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Name(null!));
    }

    [Fact]
    public void Constructor_WithExceedingLength_ThrowsArgumentException()
    {
        // Arrange
        var longName = new string('a', 101);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Name(longName));
    }

    [Fact]
    public void Constructor_WithMaxLength_CreatesName()
    {
        // Arrange
        var maxName = new string('a', 100);

        // Act
        var name = new Name(maxName);

        // Assert
        Assert.Equal(maxName, name.Value);
    }

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var name1 = new Name("John Doe");
        var name2 = new Name("John Doe");

        // Act & Assert
        Assert.Equal(name1, name2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var name1 = new Name("John Doe");
        var name2 = new Name("Jane Doe");

        // Act & Assert
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var name = new Name("John Doe");

        // Act & Assert
        Assert.Equal("John Doe", name.ToString());
    }
}
