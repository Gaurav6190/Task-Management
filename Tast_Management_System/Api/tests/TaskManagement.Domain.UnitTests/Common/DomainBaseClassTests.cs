using TaskManagement.Domain.Common;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Domain.UnitTests.Common;

[TestFixture]
public class DomainBaseClassTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return amount;
            yield return currency;
        }
    }

    private sealed class Person : Entity<Guid>
    {
        public Person(Guid id) => Id = id;
    }

    private sealed class Robot : Entity<Guid>
    {
        public Robot(Guid id) => Id = id;
    }

    // ---------- Entity ----------

    [Test]
    public void Entities_with_the_same_id_and_type_are_equal()
    {
        var id = Guid.NewGuid();
        var first = new Person(id);
        var second = new Person(id);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        });
    }

    [Test]
    public void Entities_with_different_ids_are_not_equal()
    {
        var first = new Person(Guid.NewGuid());
        var second = new Person(Guid.NewGuid());

        Assert.That(first, Is.Not.EqualTo(second));
    }

    [Test]
    public void Entities_of_different_types_with_the_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();

        Assert.That(new Person(id).Equals(new Robot(id)), Is.False);
    }

    [Test]
    public void Entity_is_not_equal_to_null_or_other_objects()
    {
        var person = new Person(Guid.NewGuid());
        // Order matters: the compiler treats x.Equals(null) as a null test on x.
        var equalsOtherType = person.Equals((object)"not an entity");
        var equalsNull = person.Equals((Person?)null);

        Assert.Multiple(() =>
        {
            Assert.That(equalsNull, Is.False);
            Assert.That(equalsOtherType, Is.False);
        });
    }

    // ---------- ValueObject ----------

    [Test]
    public void Value_objects_with_the_same_components_are_equal()
    {
        var first = new Money(10m, "EUR");
        var second = new Money(10m, "EUR");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        });
    }

    [TestCase(10, "USD")]
    [TestCase(11, "EUR")]
    public void Value_objects_with_different_components_are_not_equal(decimal amount, string currency) =>
        Assert.That(new Money(10m, "EUR"), Is.Not.EqualTo(new Money(amount, currency)));

    [Test]
    public void Value_object_is_not_equal_to_null() =>
        Assert.That(new Money(1m, "EUR").Equals(null), Is.False);

    // ---------- TaskItemId / DomainException ----------

    [Test]
    public void TaskItemId_New_creates_unique_ids_and_prints_the_guid()
    {
        var a = TaskItemId.New();
        var b = TaskItemId.New();

        Assert.Multiple(() =>
        {
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(a.ToString(), Is.EqualTo(a.Value.ToString()));
        });
    }

    [Test]
    public void DomainException_has_default_code_and_accepts_a_specific_one()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new DomainException("x").Code, Is.EqualTo("Domain.RuleViolated"));
            Assert.That(new DomainException("x", "Task.Locked").Code, Is.EqualTo("Task.Locked"));
            Assert.That(new DomainException("message").Message, Is.EqualTo("message"));
        });
    }
}
