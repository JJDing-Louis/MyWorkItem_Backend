namespace MyWorkItem.Domain.Entities;

public sealed record Function(Guid FunctionId, string Code, string Name, bool IsEnabled);
