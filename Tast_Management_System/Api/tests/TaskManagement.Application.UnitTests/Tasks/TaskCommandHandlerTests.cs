using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks.CreateTask;
using TaskManagement.Application.Tasks.DeleteTask;
using TaskManagement.Application.Tasks.UpdateTask;
using TaskManagement.Domain.Tasks;

namespace TaskManagement.Application.UnitTests.Tasks;

[TestFixture]
public class TaskCommandHandlerTests
{
    private FakeTaskItemRepository _repository = null!;
    private FakeUnitOfWork _unitOfWork = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeTaskItemRepository();
        _unitOfWork = new FakeUnitOfWork();
    }

    private CreateTaskCommandHandler Create() =>
        new(_repository, _unitOfWork, new CreateTaskCommandValidator(TimeProvider.System), TimeProvider.System);

    private UpdateTaskCommandHandler Update() =>
        new(_repository, _unitOfWork, new UpdateTaskCommandValidator(), TimeProvider.System);

    private DeleteTaskCommandHandler Delete() => new(_repository, _unitOfWork, TimeProvider.System);

    // ---------- Create ----------

    [Test]
    public async Task Create_valid_command_adds_task_saves_once_and_returns_its_id()
    {
        var result = await Create().Handle(
            new CreateTaskCommand("Write spec", "Draft", "Medium", "alice", null), CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        var added = _repository.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(added.Id.Value));
            Assert.That(added.AssignedTo, Is.EqualTo("alice"));
            Assert.That(added.Status, Is.EqualTo(TaskItemStatus.ToDo));
            Assert.That(_unitOfWork.SaveCount, Is.EqualTo(1));
        });
    }

    [TestCase("high", TaskPriority.High)]
    [TestCase(" Critical ", TaskPriority.Critical)]
    public async Task Create_parses_priority_names_case_insensitively(string priority, TaskPriority expected)
    {
        await Create().Handle(new CreateTaskCommand("Title", null, priority, null, null), CancellationToken.None);

        Assert.That(_repository.Items.Single().Priority, Is.EqualTo(expected));
    }

    [Test]
    public async Task Create_invalid_command_returns_validation_error_without_saving()
    {
        var result = await Create().Handle(new CreateTaskCommand("", null, "Urgent", null, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ValidationFailed));
            Assert.That(result.Error.ValidationErrors!.Keys, Is.EquivalentTo(new[] { "Title", "Priority" }));
            Assert.That(_repository.Items, Is.Empty);
            Assert.That(_unitOfWork.SaveCount, Is.Zero);
        });
    }

    // ---------- Update ----------

    [Test]
    public async Task Update_changes_task_and_saves()
    {
        var task = _repository.Seed();

        var result = await Update().Handle(
            new UpdateTaskCommand(task.Id.Value, "Renamed", "New", "inprogress", "High", "bob", null),
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(task.Title, Is.EqualTo("Renamed"));
            Assert.That(task.Status, Is.EqualTo(TaskItemStatus.InProgress));
            Assert.That(task.Priority, Is.EqualTo(TaskPriority.High));
            Assert.That(task.AssignedTo, Is.EqualTo("bob"));
            Assert.That(task.ModifiedDate, Is.Not.Null);
            Assert.That(_unitOfWork.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Update_unknown_task_returns_not_found_without_saving()
    {
        var id = Guid.NewGuid();

        var result = await Update().Handle(
            new UpdateTaskCommand(id, "Title", null, "ToDo", "Low", null, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error.Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.TaskNotFound));
            Assert.That(result.Error.Message, Does.Contain(id.ToString()));
            Assert.That(_unitOfWork.SaveCount, Is.Zero);
        });
    }

    [Test]
    public async Task Update_soft_deleted_task_returns_not_found()
    {
        var task = _repository.Seed();
        task.Delete(DateTime.UtcNow);

        var result = await Update().Handle(
            new UpdateTaskCommand(task.Id.Value, "Title", null, "ToDo", "Low", null, null), CancellationToken.None);

        Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.TaskNotFound));
    }

    [Test]
    public async Task Update_invalid_command_is_rejected_before_loading_the_task()
    {
        var result = await Update().Handle(
            new UpdateTaskCommand(Guid.NewGuid(), "Title", null, "Open", "Low", null, null), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.ValidationFailed));
            Assert.That(_repository.GetCalls, Is.Zero);
        });
    }

    // ---------- Delete ----------

    [Test]
    public async Task Delete_soft_deletes_and_keeps_the_task()
    {
        var task = _repository.Seed();

        var result = await Delete().Handle(new DeleteTaskCommand(task.Id.Value), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(task.IsDeleted, Is.True);
            Assert.That(_repository.Items, Does.Contain(task));
            Assert.That(_unitOfWork.SaveCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Deleting_twice_returns_not_found()
    {
        var task = _repository.Seed();
        await Delete().Handle(new DeleteTaskCommand(task.Id.Value), CancellationToken.None);

        var result = await Delete().Handle(new DeleteTaskCommand(task.Id.Value), CancellationToken.None);

        Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.TaskNotFound));
    }

    [Test]
    public async Task Delete_unknown_task_returns_not_found_without_saving()
    {
        var result = await Delete().Handle(new DeleteTaskCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error.Type, Is.EqualTo(ErrorType.NotFound));
            Assert.That(_unitOfWork.SaveCount, Is.Zero);
        });
    }
}
