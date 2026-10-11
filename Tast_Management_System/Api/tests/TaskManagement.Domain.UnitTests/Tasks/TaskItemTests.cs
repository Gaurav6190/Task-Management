using TaskManagement.Domain.Common;
using TaskManagement.Domain.Tasks;
using TaskManagement.Domain.Tasks.Events;

namespace TaskManagement.Domain.UnitTests.Tasks;

[TestFixture]
public class TaskItemTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddHours(1);

    private static TaskItem NewTask(DateTime? dueDateUtc = null) =>
        TaskItem.Create("Write spec", "Draft", TaskPriority.High, "alice", dueDateUtc, Now);

    private static void UpdateStatus(TaskItem task, TaskItemStatus status, DateTime at) =>
        task.Update(task.Title, task.Description, status, task.Priority, task.AssignedTo, task.DueDateUtc, at);

    // ---------- Create ----------

    [Test]
    public void Create_sets_defaults_and_raises_created_event()
    {
        var task = NewTask();

        Assert.Multiple(() =>
        {
            Assert.That(task.Id.Value, Is.Not.EqualTo(Guid.Empty));
            Assert.That(task.Status, Is.EqualTo(TaskItemStatus.ToDo));
            Assert.That(task.Priority, Is.EqualTo(TaskPriority.High));
            Assert.That(task.AssignedTo, Is.EqualTo("alice"));
            Assert.That(task.CreatedDate, Is.EqualTo(Now));
            Assert.That(task.ModifiedDate, Is.Null);
            Assert.That(task.CompletedAtUtc, Is.Null);
            Assert.That(task.IsDeleted, Is.False);
            Assert.That(task.DeletedDate, Is.Null);
            Assert.That(task.DomainEvents.OfType<TaskItemCreated>().Single().TaskId, Is.EqualTo(task.Id));
        });
    }

    [Test]
    public void Create_trims_title_description_and_assignee()
    {
        var task = TaskItem.Create("  Title  ", "  Desc  ", TaskPriority.Low, "  bob  ", null, Now);

        Assert.Multiple(() =>
        {
            Assert.That(task.Title, Is.EqualTo("Title"));
            Assert.That(task.Description, Is.EqualTo("Desc"));
            Assert.That(task.AssignedTo, Is.EqualTo("bob"));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Create_with_blank_title_throws(string title)
    {
        var ex = Assert.Throws<DomainException>(() => TaskItem.Create(title, null, TaskPriority.Low, null, null, Now));
        Assert.That(ex!.Message, Is.EqualTo("Title is required."));
    }

    [Test]
    public void Create_accepts_values_exactly_at_the_maximum_lengths()
    {
        var task = TaskItem.Create(
            new string('t', TaskItem.TitleMaxLength), new string('d', TaskItem.DescriptionMaxLength),
            TaskPriority.Low, new string('a', TaskItem.AssignedToMaxLength), null, Now);

        Assert.That(task.Title, Has.Length.EqualTo(TaskItem.TitleMaxLength));
    }

    [Test]
    public void Create_with_too_long_title_throws() =>
        Assert.Throws<DomainException>(() =>
            TaskItem.Create(new string('t', TaskItem.TitleMaxLength + 1), null, TaskPriority.Low, null, null, Now));

    [Test]
    public void Create_with_too_long_description_throws() =>
        Assert.Throws<DomainException>(() =>
            TaskItem.Create("Title", new string('d', TaskItem.DescriptionMaxLength + 1), TaskPriority.Low, null, null, Now));

    [Test]
    public void Create_with_too_long_assignee_throws() =>
        Assert.Throws<DomainException>(() =>
            TaskItem.Create("Title", null, TaskPriority.Low, new string('a', TaskItem.AssignedToMaxLength + 1), null, Now));

    [Test]
    public void Create_with_undefined_priority_throws() =>
        Assert.Throws<DomainException>(() => TaskItem.Create("Title", null, (TaskPriority)99, null, null, Now));

    [Test]
    public void Create_with_past_due_date_throws() =>
        Assert.Throws<DomainException>(() =>
            TaskItem.Create("Title", null, TaskPriority.Low, null, Now.AddDays(-2), Now));

    [Test]
    public void Create_with_due_date_today_is_allowed()
    {
        Assert.That(NewTask(Now.Date).DueDateUtc, Is.EqualTo(Now.Date));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Blank_assignee_is_stored_as_null(string? assignee)
    {
        Assert.That(TaskItem.Create("Title", null, TaskPriority.Low, assignee, null, Now).AssignedTo, Is.Null);
    }

    // ---------- Update ----------

    [Test]
    public void Update_changes_fields_and_sets_modified_date()
    {
        var task = NewTask();

        task.Update("New title", null, TaskItemStatus.InProgress, TaskPriority.Low, "bob", null, Later);

        Assert.Multiple(() =>
        {
            Assert.That(task.Title, Is.EqualTo("New title"));
            Assert.That(task.Description, Is.Null);
            Assert.That(task.Status, Is.EqualTo(TaskItemStatus.InProgress));
            Assert.That(task.Priority, Is.EqualTo(TaskPriority.Low));
            Assert.That(task.AssignedTo, Is.EqualTo("bob"));
            Assert.That(task.ModifiedDate, Is.EqualTo(Later));
            Assert.That(task.CreatedDate, Is.EqualTo(Now));
        });
    }

    [Test]
    public void Moving_to_done_sets_completed_date_and_raises_event()
    {
        var task = NewTask();

        UpdateStatus(task, TaskItemStatus.Done, Later);

        Assert.Multiple(() =>
        {
            Assert.That(task.Status, Is.EqualTo(TaskItemStatus.Done));
            Assert.That(task.CompletedAtUtc, Is.EqualTo(Later));
            Assert.That(task.DomainEvents.OfType<TaskItemCompleted>().Count(), Is.EqualTo(1));
        });
    }

    [TestCase(TaskItemStatus.ToDo)]
    [TestCase(TaskItemStatus.InProgress)]
    public void Moving_out_of_done_clears_completed_date(TaskItemStatus newStatus)
    {
        var task = NewTask();
        UpdateStatus(task, TaskItemStatus.Done, Later);

        UpdateStatus(task, newStatus, Later);

        Assert.That(task.CompletedAtUtc, Is.Null);
    }

    [Test]
    public void Keeping_the_same_status_raises_no_extra_event()
    {
        var task = NewTask();
        UpdateStatus(task, TaskItemStatus.Done, Later);

        UpdateStatus(task, TaskItemStatus.Done, Later.AddHours(1));

        Assert.Multiple(() =>
        {
            Assert.That(task.DomainEvents.OfType<TaskItemCompleted>().Count(), Is.EqualTo(1));
            Assert.That(task.CompletedAtUtc, Is.EqualTo(Later), "original completion time is kept");
        });
    }

    [Test]
    public void Update_with_undefined_status_throws() =>
        Assert.Throws<DomainException>(() => UpdateStatus(NewTask(), (TaskItemStatus)99, Later));

    [Test]
    public void Update_with_invalid_details_throws_and_keeps_the_task_unchanged()
    {
        var task = NewTask();

        Assert.Throws<DomainException>(() =>
            task.Update("", null, TaskItemStatus.Done, TaskPriority.Low, null, null, Later));
        Assert.Multiple(() =>
        {
            Assert.That(task.Title, Is.EqualTo("Write spec"));
            Assert.That(task.Status, Is.EqualTo(TaskItemStatus.ToDo));
            Assert.That(task.ModifiedDate, Is.Null);
        });
    }

    [Test]
    public void Overdue_task_can_be_edited_without_changing_due_date()
    {
        var dueDate = Now.AddDays(1);
        var task = NewTask(dueDate);

        task.Update("Renamed", null, TaskItemStatus.InProgress, TaskPriority.Low, null, dueDate, Now.AddDays(10));

        Assert.That(task.Title, Is.EqualTo("Renamed"));
    }

    [Test]
    public void Changing_the_due_date_to_the_past_throws()
    {
        var task = NewTask(Now.AddDays(5));

        Assert.Throws<DomainException>(() =>
            task.Update("Title", null, TaskItemStatus.ToDo, TaskPriority.Low, null, Now.AddDays(-3), Now));
    }

    [Test]
    public void Due_date_can_be_removed()
    {
        var task = NewTask(Now.AddDays(5));

        task.Update("Title", null, TaskItemStatus.ToDo, TaskPriority.Low, null, null, Later);

        Assert.That(task.DueDateUtc, Is.Null);
    }

    // ---------- Delete ----------

    [Test]
    public void Delete_marks_task_as_deleted_and_raises_event()
    {
        var task = NewTask();

        task.Delete(Later);

        Assert.Multiple(() =>
        {
            Assert.That(task.IsDeleted, Is.True);
            Assert.That(task.DeletedDate, Is.EqualTo(Later));
            Assert.That(task.ModifiedDate, Is.EqualTo(Later));
            Assert.That(task.DomainEvents.OfType<TaskItemDeleted>().Single().OccurredOnUtc, Is.EqualTo(Later));
        });
    }

    [Test]
    public void Deleted_task_cannot_be_updated()
    {
        var task = NewTask();
        task.Delete(Later);

        var ex = Assert.Throws<DomainException>(() => UpdateStatus(task, TaskItemStatus.Done, Later));
        Assert.That(ex!.Message, Is.EqualTo("A deleted task cannot be modified."));
    }

    [Test]
    public void Deleted_task_cannot_be_deleted_again()
    {
        var task = NewTask();
        task.Delete(Later);

        Assert.Throws<DomainException>(() => task.Delete(Later));
    }

    [Test]
    public void ClearDomainEvents_empties_the_event_list()
    {
        var task = NewTask();

        task.ClearDomainEvents();

        Assert.That(task.DomainEvents, Is.Empty);
    }
}
