namespace TaskManagement.Domain.Tasks;

public readonly record struct TaskItemId(Guid Value)
{
    public static TaskItemId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
