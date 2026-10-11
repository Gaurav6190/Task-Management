using TaskManagement.Domain.Common;

namespace TaskManagement.Domain.Tasks.Events;

public sealed record TaskItemCreated(TaskItemId TaskId, DateTime OccurredOnUtc) : IDomainEvent;

public sealed record TaskItemCompleted(TaskItemId TaskId, DateTime OccurredOnUtc) : IDomainEvent;

public sealed record TaskItemDeleted(TaskItemId TaskId, DateTime OccurredOnUtc) : IDomainEvent;
