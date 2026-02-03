namespace Domain.ValueObjects;

public sealed class Name
{
    public string Value { get; }

    public Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Name cannot be empty");

        if (value.Length > 100)
            throw new ArgumentException("Name cannot exceed 100 characters");

        Value = value.Trim();
    }

    public override string ToString() => Value;

    public override bool Equals(object? obj) =>
        obj is Name other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();
}
