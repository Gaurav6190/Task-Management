import { ApiError, apiRequest } from '../../api/httpClient'
import {
  DEFAULT_PAGE_SIZE,
  type CreateTaskRequest,
  type ListTasksParams,
  type PagedResult,
  type TaskDto,
  type UpdateTaskRequest,
} from './taskTypes'

// The only module that calls /api/tasks. Components and hooks go through these functions.

export function listTasks(params: ListTasksParams, token: string, signal?: AbortSignal): Promise<PagedResult<TaskDto>> {
  const query = new URLSearchParams({
    page: String(params.page ?? 1),
    pageSize: String(params.pageSize ?? DEFAULT_PAGE_SIZE),
  })
  if (params.status) query.set('status', params.status)
  if (params.priority) query.set('priority', params.priority)

  return apiRequest<PagedResult<TaskDto>>(`/api/tasks?${query}`, { token, signal })
}

/** One task as currently stored in the database. Used to refresh a row after a save. */
export function getTask(id: string, token: string): Promise<TaskDto> {
  return apiRequest<TaskDto>(`/api/tasks/${encodeURIComponent(id)}`, { token })
}

/** Creates a task and returns its id (201 Created, body `{ id }`). */
export async function createTask(body: CreateTaskRequest, token: string): Promise<string> {
  const created = await apiRequest<{ id: string }>('/api/tasks', {
    method: 'POST',
    body: JSON.stringify(body),
    token,
  })
  return created.id
}

/** Replaces a task (204 No Content). Build the body with `toUpdateRequest` so no field is dropped. */
export function updateTask(id: string, body: UpdateTaskRequest, token: string): Promise<void> {
  return apiRequest<void>(`/api/tasks/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body: JSON.stringify(body),
    token,
  })
}

/**
 * PUT replaces the whole task, so copy every field from the current task and apply the changes.
 * `dueDateUtc` is passed back exactly as received: the API only rejects a past due date when it changes,
 * so an overdue task can still have its status updated.
 */
export function toUpdateRequest(task: TaskDto, changes: Partial<UpdateTaskRequest> = {}): UpdateTaskRequest {
  return {
    title: task.title,
    description: task.description,
    status: task.status,
    priority: task.priority,
    assignedTo: task.assignedTo,
    dueDateUtc: task.dueDateUtc,
    ...changes,
  }
}

/** 401 means the token is missing or expired; the caller should sign the user out. */
export function isUnauthorized(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401
}
