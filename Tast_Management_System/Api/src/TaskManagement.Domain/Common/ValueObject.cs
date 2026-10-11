namespace TaskManagement.Domain.Common;

public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other) =>
        other is not null && other.GetType() == GetType() &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override bool Equals(object? obj) => obj is ValueObject v && Equals(v);
    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, (hash, c) => HashCode.Combine(hash, c));
}
