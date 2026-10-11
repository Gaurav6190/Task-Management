using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Tasks.GetTaskById;
using TaskManagement.Application.Tasks.GetTaskSummary;
using TaskManagement.Application.Tasks.ListTasks;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.UnitTests.Tasks;

[TestFixture]
public class TaskQueryHandlerTests
{
    // ---------- GetTaskById ----------

    [Test]
    public async Task GetById_existing_task_returns_every_field_mapped()
    {
        var repository = new FakeTaskItemRepository();
        var due = DateTime.UtcNow.Date.AddDays(3);
        var task = TaskItem.Create("Title", "Desc", TaskPriority.Critical, "carol", due, DateTime.UtcNow);
        repository.Add(task);

        var result = await new GetTaskByIdQueryHandler(repository)
            .Handle(new GetTaskByIdQuery(task.Id.Value), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(new TaskDto(
            task.Id.Value, "Title", "Desc", "ToDo", "Critical", "carol", due, task.CreatedDate, null, null)));
    }

    [Test]
    public async Task GetById_unknown_task_returns_task_not_found()
    {
        var result = await new GetTaskByIdQueryHandler(new FakeTaskItemRepository())
            .Handle(new GetTaskByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.TaskNotFound));
        });
    }

    // ---------- ListTasks ----------

    [Test]
    public async Task List_valid_query_passes_parsed_filters_and_paging_to_the_read_side()
    {
        var queries = new FakeTaskQueries { Result = new PagedResult<TaskDto>([], 2, 5, 7) };

        var result = await new ListTasksQueryHandler(queries, new ListTasksQueryValidator())
            .Handle(new ListTasksQuery("done", "HIGH", 2, 5), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.SameAs(queries.Result));
            Assert.That(queries.LastCall, Is.EqualTo(((TaskItemStatus?)TaskItemStatus.Done, (TaskPriority?)TaskPriority.High, 2, 5)));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public async Task List_without_filters_passes_null_filters(string? filter)
    {
        var queries = new FakeTaskQueries();

        await new ListTasksQueryHandler(queries, new ListTasksQueryValidator())
            .Handle(new ListTasksQuery(filter, filter), CancellationToken.None);

        Assert.That(queries.LastCall, Is.EqualTo(((TaskItemStatus?)null, (TaskPriority?)null, 1, 20)));
    }

    [Test]
    public async Task List_invalid_query_returns_validation_error_and_does_not_query()
    {
        var queries = new FakeTaskQueries();

        var result = await new ListTasksQueryHandler(queries, new ListTasksQueryValidator())
            .Handle(new ListTasksQuery("Bogus", null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ValidationFailed));
            Assert.That(queries.LastCall, Is.Null);
        });
    }

    // ---------- GetTaskSummary ----------

    private static async Task<TaskSummaryDto> Summarize(params TaskSummaryRow[] rows)
    {
        var result = await new GetTaskSummaryQueryHandler(new FakeSummaryQuery(rows))
            .Handle(new GetTaskSummaryQuery(), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        return result.Value;
    }

    [Test]
    public async Task Summary_of_empty_database_has_all_12_combinations_with_zero()
    {
        var summary = await Summarize();

        Assert.Multiple(() =>
        {
            Assert.That(summary.Total, Is.Zero);
            Assert.That(summary.ByStatusAndPriority, Has.Count.EqualTo(12));
            Assert.That(summary.ByStatusAndPriority.Select(c => c.Count), Is.All.Zero);
            Assert.That(summary.ByStatus.Keys, Is.EqualTo(new[] { "ToDo", "InProgress", "Done" }));
            Assert.That(summary.ByPriority.Keys, Is.EqualTo(new[] { "Low", "Medium", "High", "Critical" }));
        });
    }

    [Test]
    public async Task Summary_fills_missing_combinations_with_zero_in_enum_order()
    {
        var summary = await Summarize(new TaskSummaryRow(TaskItemStatus.Done, TaskPriority.High, 4));

        Assert.Multiple(() =>
        {
            Assert.That(summary.ByStatusAndPriority[0], Is.EqualTo(new TaskSummaryItem("ToDo", "Low", 0)));
            Assert.That(summary.ByStatusAndPriority[^1], Is.EqualTo(new TaskSummaryItem("Done", "Critical", 0)));
            Assert.That(summary.ByStatusAndPriority, Does.Contain(new TaskSummaryItem("Done", "High", 4)));
            Assert.That(summary.ByStatusAndPriority.Count(c => c.Count == 0), Is.EqualTo(11));
        });
    }

    [Test]
    public async Task Summary_totals_are_the_sums_of_the_grouped_counts()
    {
        var summary = await Summarize(
            new TaskSummaryRow(TaskItemStatus.ToDo, TaskPriority.Low, 3),
            new TaskSummaryRow(TaskItemStatus.ToDo, TaskPriority.High, 2),
            new TaskSummaryRow(TaskItemStatus.InProgress, TaskPriority.High, 5),
            new TaskSummaryRow(TaskItemStatus.Done, TaskPriority.Critical, 1));

        Assert.Multiple(() =>
        {
            Assert.That(summary.Total, Is.EqualTo(11));
            Assert.That(summary.ByStatus.Values.Sum(), Is.EqualTo(11));
            Assert.That(summary.ByPriority.Values.Sum(), Is.EqualTo(11));
            Assert.That(summary.ByStatus["ToDo"], Is.EqualTo(5));
            Assert.That(summary.ByStatus["InProgress"], Is.EqualTo(5));
            Assert.That(summary.ByStatus["Done"], Is.EqualTo(1));
            Assert.That(summary.ByPriority["High"], Is.EqualTo(7));
            Assert.That(summary.ByPriority["Medium"], Is.Zero);
        });
    }

    [Test]
    public async Task Summary_adds_up_duplicate_rows_for_the_same_combination()
    {
        var summary = await Summarize(
            new TaskSummaryRow(TaskItemStatus.ToDo, TaskPriority.Low, 2),
            new TaskSummaryRow(TaskItemStatus.ToDo, TaskPriority.Low, 3));

        Assert.That(summary.ByStatusAndPriority[0].Count, Is.EqualTo(5));
    }
}
