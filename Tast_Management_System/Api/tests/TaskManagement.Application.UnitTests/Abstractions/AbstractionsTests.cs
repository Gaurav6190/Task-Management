using FluentValidation.Results;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.UnitTests.Abstractions;

[TestFixture]
public class AbstractionsTests
{
    // ---------- Result ----------

    [Test]
    public void Success_result_has_no_error()
    {
        var result = Result.Success();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.False);
            Assert.That(result.Error, Is.EqualTo(Error.None));
        });
    }

    [Test]
    public void Failure_result_carries_the_error()
    {
        var error = Error.Conflict("X.Conflict", "conflict");

        Result result = error; // implicit conversion

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(Result.Failure(error).Error, Is.EqualTo(error));
        });
    }

    [Test]
    public void Generic_result_converts_from_value_and_from_error()
    {
        Result<int> ok = 42;
        Result<int> failed = Error.NotFound("X.NotFound", "missing");

        Assert.Multiple(() =>
        {
            Assert.That(ok.Value, Is.EqualTo(42));
            Assert.That(failed.IsFailure, Is.True);
            Assert.That(Result.Failure<int>(failed.Error).Error.Type, Is.EqualTo(ErrorType.NotFound));
        });
    }

    [Test]
    public void Reading_the_value_of_a_failed_result_throws()
    {
        Result<int> failed = Error.NotFound("X.NotFound", "missing");

        Assert.Throws<InvalidOperationException>(() => _ = failed.Value);
    }

    [Test]
    public void Error_factories_set_the_type()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Error.NotFound("a", "b").Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That(Error.Conflict("a", "b").Type, Is.EqualTo(ErrorType.Conflict));
            Assert.That(Error.Unauthorized("a", "b").Type, Is.EqualTo(ErrorType.Unauthorized));
            var validation = Error.Validation("msg", new Dictionary<string, string[]> { ["Title"] = ["x"] });
            Assert.That(validation.Type, Is.EqualTo(ErrorType.Validation));
            Assert.That(validation.Code, Is.EqualTo(ErrorCodes.ValidationFailed));
        });
    }

    // ---------- Validation helpers ----------

    [Test]
    public void ToError_groups_messages_per_property_and_removes_duplicates()
    {
        var result = new ValidationResult(
        [
            new ValidationFailure("Title", "Title is required."),
            new ValidationFailure("Title", "Title is required."),
            new ValidationFailure("Title", "Title is too short."),
            new ValidationFailure("Status", "Status is required.")
        ]);

        var error = result.ToError();

        Assert.Multiple(() =>
        {
            Assert.That(error.Message, Is.EqualTo(ValidationExtensions.ValidationMessage));
            Assert.That(error.ValidationErrors!["Title"], Is.EqualTo(new[] { "Title is required.", "Title is too short." }));
            Assert.That(error.ValidationErrors["Status"], Is.EqualTo(new[] { "Status is required." }));
        });
    }

    [TestCase("ToDo", true)]
    [TestCase("todo", true)]
    [TestCase(" Done ", true)]
    [TestCase("Open", false)]
    [TestCase("0", false)]
    [TestCase("ToDo, Done", false)]
    public void EnumNames_IsValid_accepts_member_names_only(string value, bool expected) =>
        Assert.That(EnumNames.IsValid<TaskItemStatus>(value), Is.EqualTo(expected));

    [Test]
    public void EnumNames_parses_and_lists_names()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EnumNames.Parse<TaskPriority>(" critical "), Is.EqualTo(TaskPriority.Critical));
            Assert.That(EnumNames.ParseOptional<TaskPriority>("low"), Is.EqualTo(TaskPriority.Low));
            Assert.That(EnumNames.ParseOptional<TaskPriority>(null), Is.Null);
            Assert.That(EnumNames.ParseOptional<TaskPriority>(" "), Is.Null);
            Assert.That(EnumNames.List<TaskItemStatus>(), Is.EqualTo("ToDo, InProgress, Done"));
        });
    }

    // ---------- PagedResult / TaskDto / TaskErrors ----------

    [TestCase(0, 20, 0)]
    [TestCase(1, 20, 1)]
    [TestCase(20, 20, 1)]
    [TestCase(21, 20, 2)]
    [TestCase(27, 5, 6)]
    public void PagedResult_TotalPages_rounds_up(int totalCount, int pageSize, int expectedPages) =>
        Assert.That(new PagedResult<int>([], 1, pageSize, totalCount).TotalPages, Is.EqualTo(expectedPages));

    [Test]
    public void TaskDto_maps_a_completed_task()
    {
        var now = DateTime.UtcNow;
        var task = TaskItem.Create("T", null, TaskPriority.Low, null, null, now);
        task.Update("T", null, TaskItemStatus.Done, TaskPriority.Low, null, null, now);

        var dto = TaskDto.FromDomain(task);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Status, Is.EqualTo("Done"));
            Assert.That(dto.CompletedAtUtc, Is.EqualTo(now));
            Assert.That(dto.ModifiedDate, Is.EqualTo(now));
        });
    }

    [Test]
    public void TaskErrors_NotFound_names_the_task()
    {
        var id = Guid.NewGuid();

        Assert.That(TaskErrors.NotFound(id),
            Is.EqualTo(new Error(ErrorCodes.TaskNotFound, $"Task '{id}' was not found.", ErrorType.NotFound)));
    }
}
