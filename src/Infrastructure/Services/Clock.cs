using Application.Services;

namespace Infrastructure.Services;

public class Clock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
