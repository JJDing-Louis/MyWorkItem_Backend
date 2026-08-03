using MyWorkItem.Application.Abstractions;

namespace MyWorkItem.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
