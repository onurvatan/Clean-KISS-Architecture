namespace Application.Services;

public interface IClock
{
    DateTime UtcNow { get; }
}
