using TaskManagement.Application.Tasks.CreateTask;
using TaskManagement.Application.Tasks.ListTasks;
using TaskManagement.Application.Tasks.UpdateTask;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.UnitTests.Tasks;

[TestFixture]
public class TaskValidatorTests
{
    private static readonly CreateTaskCommandValidator CreateValidator = new(TimeProvider.System);
    private static readonly UpdateTaskCommandValidator UpdateValidator = new();
    private static readonly ListTasksQueryValidator ListValidator = new();

    private static CreateTaskCommand ValidCreate() => new("Title", null, "High", null, null);
    private static UpdateTaskCommand ValidUpdate() => new(Guid.NewGuid(), "Title", null, "ToDo", "Low", null, null);

    private static (string Property, string Message) SingleError(FluentValidation.Results.ValidationResult result)
    {
        Assert.That(result.Errors, Has.Count.EqualTo(1), string.Join("; ", result.Errors));
        return (result.Errors[0].PropertyName, result.Errors[0].ErrorMessage);
    }

    // ---------- Create ----------

    [Test]
    public void Create_valid_command_passes() => Assert.That(CreateValidator.Validate(ValidCreate()).IsValid, Is.True);

    [Test]
    public void Create_accepts_values_at_the_maximum_lengths_and_a_due_date_today()
    {
        var command = new CreateTaskCommand(
            new string('t', TaskItem.TitleMaxLength), new string('d', TaskItem.DescriptionMaxLength), "Low",
            new string('a', TaskItem.AssignedToMaxLength), DateTime.UtcNow.Date);

        Assert.That(CreateValidator.Validate(command).IsValid, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Create_title_is_required(string? title) =>
        Assert.That(SingleError(CreateValidator.Validate(ValidCreate() with { Title = title })),
            Is.EqualTo(("Title", "Title is required.")));

    [Test]
    public void Create_title_longer_than_200_is_rejected() =>
        Assert.That(SingleError(CreateValidator.Validate(ValidCreate() with { Title = new string('x', 201) })),
            Is.EqualTo(("Title", "Title cannot exceed 200 characters.")));

    [Test]
    public void Create_description_longer_than_4000_is_rejected() =>
        Assert.That(SingleError(CreateValidator.Validate(ValidCreate() with { Description = new string('x', 4001) })).Property,
            Is.EqualTo("Description"));

    [Test]
    public void Create_assignee_longer_than_100_is_rejected() =>
        Assert.That(SingleError(CreateValidator.Validate(ValidCreate() with { AssignedTo = new string('x', 101) })).Property,
            Is.EqualTo("AssignedTo"));

    [TestCase("High")]
    [TestCase("high")]
    [TestCase(" Critical ")]
    public void Create_priority_accepts_enum_names_case_insensitively(string priority) =>
        Assert.That(CreateValidator.Validate(ValidCreate() with { Priority = priority }).IsValid, Is.True);

    [TestCase("Urgent")]
    [TestCase("2")]
    [TestCase("Low, High")]
    public void Create_priority_rejects_unknown_names_numbers_and_combinations(string priority) =>
        Assert.That(SingleError(CreateValidator.Validate(ValidCreate() with { Priority = priority })),
            Is.EqualTo(("Priority", "Priority must be one of: Low, Medium, High, Critical.")));

    [TestCase(null)]
    [TestCase("")]
    public void Create_priority_is_required(string? priority) =>
        Assert.That(SingleError(CreateValidator.Validate(ValidCreate() with { Priority = priority })),
            Is.EqualTo(("Priority", "Priority is required.")));

    [Test]
    public void Create_due_date_in_the_past_is_rejected() =>
        Assert.That(SingleError(CreateValidator.Validate(ValidCreate() with { DueDateUtc = DateTime.UtcNow.AddDays(-2) })),
            Is.EqualTo(("DueDateUtc", "DueDateUtc cannot be in the past.")));

    // ---------- Update ----------

    [Test]
    public void Update_valid_command_passes() => Assert.That(UpdateValidator.Validate(ValidUpdate()).IsValid, Is.True);

    [Test]
    public void Update_requires_an_id() =>
        Assert.That(SingleError(UpdateValidator.Validate(ValidUpdate() with { Id = Guid.Empty })),
            Is.EqualTo(("Id", "Id is required.")));

    [TestCase(null, "Status is required.")]
    [TestCase("", "Status is required.")]
    [TestCase("Open", "Status must be one of: ToDo, InProgress, Done.")]
    [TestCase("1", "Status must be one of: ToDo, InProgress, Done.")]
    public void Update_requires_a_valid_status(string? status, string message) =>
        Assert.That(SingleError(UpdateValidator.Validate(ValidUpdate() with { Status = status })),
            Is.EqualTo(("Status", message)));

    [TestCase(null, "Priority is required.")]
    [TestCase("Urgent", "Priority must be one of: Low, Medium, High, Critical.")]
    public void Update_requires_a_valid_priority(string? priority, string message) =>
        Assert.That(SingleError(UpdateValidator.Validate(ValidUpdate() with { Priority = priority })),
            Is.EqualTo(("Priority", message)));

    [Test]
    public void Update_rejects_blank_title_and_too_long_fields()
    {
        var result = UpdateValidator.Validate(ValidUpdate() with
        {
            Title = " ", Description = new string('x', 4001), AssignedTo = new string('x', 101)
        });

        Assert.That(result.Errors.Select(e => e.PropertyName),
            Is.EquivalentTo(new[] { "Title", "Description", "AssignedTo" }));
    }

    [Test]
    public void Update_allows_past_due_date_so_overdue_tasks_stay_editable() =>
        Assert.That(UpdateValidator.Validate(ValidUpdate() with { DueDateUtc = DateTime.UtcNow.AddDays(-5) }).IsValid, Is.True);

    // ---------- List ----------

    [Test]
    public void List_filters_are_optional() =>
        Assert.That(ListValidator.Validate(new ListTasksQuery(null, null)).IsValid, Is.True);

    [TestCase("ToDo", "Low", 1, 1)]
    [TestCase("done", "CRITICAL", 3, 100)]
    public void List_accepts_valid_filters_and_paging(string status, string priority, int page, int pageSize) =>
        Assert.That(ListValidator.Validate(new ListTasksQuery(status, priority, page, pageSize)).IsValid, Is.True);

    [Test]
    public void List_rejects_bad_filters_and_paging()
    {
        var result = ListValidator.Validate(new ListTasksQuery("Bogus", "Urgent", Page: 0, PageSize: 500));

        Assert.That(result.Errors.Select(e => e.PropertyName),
            Is.EquivalentTo(new[] { "Status", "Priority", "Page", "PageSize" }));
    }

    [TestCase(0)]
    [TestCase(101)]
    public void List_page_size_must_be_between_1_and_100(int pageSize) =>
        Assert.That(SingleError(ListValidator.Validate(new ListTasksQuery(null, null, 1, pageSize))),
            Is.EqualTo(("PageSize", "PageSize must be between 1 and 100.")));
}
