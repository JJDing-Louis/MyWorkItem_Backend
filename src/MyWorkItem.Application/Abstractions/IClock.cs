namespace MyWorkItem.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
