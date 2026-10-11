using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Contracts.Tasks;
using TaskManagement.Api.Infrastructure;
using TaskManagement.Application.Abstractions;
using TaskManagement.Application.Tasks;
using TaskManagement.Application.Tasks.CreateTask;
using TaskManagement.Application.Tasks.DeleteTask;
using TaskManagement.Application.Tasks.GetTaskById;
using TaskManagement.Application.Tasks.GetTaskSummary;
using TaskManagement.Application.Tasks.ListTasks;
using TaskManagement.Application.Tasks.UpdateTask;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class TasksController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<TaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TaskDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromServices] IQueryHandler<ListTasksQuery, PagedResult<TaskDto>> handler,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await handler.Handle(new ListTasksQuery(status, priority, page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this);
    }

    [HttpGet("summary")]
    [ProducesResponseType<TaskSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskSummaryDto>> Summary(
        [FromServices] IQueryHandler<GetTaskSummaryQuery, TaskSummaryDto> handler, CancellationToken ct)
    {
        var result = await handler.Handle(new GetTaskSummaryQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TaskDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> GetById(
        Guid id, [FromServices] IQueryHandler<GetTaskByIdQuery, TaskDto> handler, CancellationToken ct)
    {
        var result = await handler.Handle(new GetTaskByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(this);
    }

    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Create(
        CreateTaskRequest request,
        [FromServices] ICommandHandler<CreateTaskCommand, Guid> handler,
        CancellationToken ct)
    {
        var command = new CreateTaskCommand(
            request.Title, request.Description, request.Priority, request.AssignedTo, request.DueDateUtc);
        var result = await handler.Handle(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new CreatedResponse(result.Value))
            : result.ToProblem(this);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update(
        Guid id,
        UpdateTaskRequest request,
        [FromServices] ICommandHandler<UpdateTaskCommand> handler,
        CancellationToken ct)
    {
        var command = new UpdateTaskCommand(
            id, request.Title, request.Description, request.Status, request.Priority,
            request.AssignedTo, request.DueDateUtc);
        var result = await handler.Handle(command, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(this);
    }

    /// <summary>Soft delete: the task is marked as deleted and hidden, not removed from the database.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        Guid id, [FromServices] ICommandHandler<DeleteTaskCommand> handler, CancellationToken ct)
    {
        var result = await handler.Handle(new DeleteTaskCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(this);
    }
}
