// Mirrors TaskManagement.Api's task contracts. Enum values travel as names ("InProgress"), never numbers.
// String-literal unions instead of `enum`: tsconfig enables erasableSyntaxOnly.

export const TASK_STATUSES = ['ToDo', 'InProgress', 'Done'] as const
export type TaskStatus = (typeof TASK_STATUSES)[number]

export const TASK_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const
export type TaskPriority = (typeof TASK_PRIORITIES)[number]

export const STATUS_LABELS: Record<TaskStatus, string> = {
  ToDo: 'To do',
  InProgress: 'In progress',
  Done: 'Done',
}

export const PRIORITY_LABELS: Record<TaskPriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Critical: 'Critical',
}

/** Same limits as TaskItem / TaskValidationRules in the API. */
export const TASK_LIMITS = {
  titleMaxLength: 200,
  descriptionMaxLength: 4000,
  assignedToMaxLength: 100,
} as const

export const DEFAULT_PAGE_SIZE = 20
/** ListTasksQueryValidator.MaxPageSize */
export const MAX_PAGE_SIZE = 100

/** GET /api/tasks items and GET /api/tasks/{id}. Dates are ISO 8601 UTC strings. */
export interface TaskDto {
  id: string
  title: string
  description: string | null
  status: TaskStatus
  priority: TaskPriority
  assignedTo: string | null
  dueDateUtc: string | null
  createdDate: string
  modifiedDate: string | null
  completedAtUtc: string | null
}

/** Paged envelope returned by GET /api/tasks (newest first). */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ListTasksParams {
  page?: number
  pageSize?: number
  status?: TaskStatus
  priority?: TaskPriority
}

/** Body of POST /api/tasks. New tasks always start as ToDo. */
export interface CreateTaskRequest {
  title: string
  description: string | null
  priority: TaskPriority
  assignedTo: string | null
  dueDateUtc: string | null
}

/** Body of PUT /api/tasks/{id}. A full replacement: every field must be sent, not just the changed ones. */
export interface UpdateTaskRequest extends CreateTaskRequest {
  status: TaskStatus
}

export function isTaskStatus(value: string): value is TaskStatus {
  return (TASK_STATUSES as readonly string[]).includes(value)
}

export function isTaskPriority(value: string): value is TaskPriority {
  return (TASK_PRIORITIES as readonly string[]).includes(value)
}
