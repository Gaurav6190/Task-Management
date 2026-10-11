namespace TaskManagement.Domain.Tasks;

/// <summary>Importance of a task. Sent and received by name ("High"), never by number.</summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
