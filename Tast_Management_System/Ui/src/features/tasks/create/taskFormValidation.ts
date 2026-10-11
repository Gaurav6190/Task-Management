import { dateInputToUtcIso, todayUtcDateInput } from '../../../lib/dates'
import { TASK_LIMITS, type CreateTaskRequest, type TaskPriority } from '../taskTypes'

/** What the form holds while the user types. `dueDate` is an `<input type="date">` value or "". */
export interface TaskFormValues {
  title: string
  description: string
  priority: TaskPriority
  assignedTo: string
  dueDate: string
}

export type TaskFormField = keyof TaskFormValues
export type TaskFormErrors = Partial<Record<TaskFormField, string>>

export const EMPTY_TASK_FORM: TaskFormValues = {
  title: '',
  description: '',
  priority: 'Medium',
  assignedTo: '',
  dueDate: '',
}

/**
 * Same rules as the API's CreateTaskCommandValidator, so invalid input is caught before a request is sent.
 * `today` is the current UTC day, because that is what the API's "not in the past" check uses.
 */
export function validateTaskForm(values: TaskFormValues, today: string = todayUtcDateInput()): TaskFormErrors {
  const errors: TaskFormErrors = {}
  const title = values.title.trim()

  if (title.length === 0) errors.title = 'Title is required.'
  else if (title.length > TASK_LIMITS.titleMaxLength)
    errors.title = `Title cannot exceed ${TASK_LIMITS.titleMaxLength} characters.`

  if (values.description.trim().length > TASK_LIMITS.descriptionMaxLength)
    errors.description = `Description cannot exceed ${TASK_LIMITS.descriptionMaxLength} characters.`

  if (values.assignedTo.trim().length > TASK_LIMITS.assignedToMaxLength)
    errors.assignedTo = `Assigned to cannot exceed ${TASK_LIMITS.assignedToMaxLength} characters.`

  if (values.dueDate !== '') {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(values.dueDate)) errors.dueDate = 'Enter a valid date.'
    else if (values.dueDate < today) errors.dueDate = 'Due date cannot be in the past.'
  }

  return errors
}

export function hasErrors(errors: TaskFormErrors): boolean {
  return Object.values(errors).some(Boolean)
}

/** Form values -> POST /api/tasks body: trimmed, with empty optional fields sent as null. */
export function toCreateRequest(values: TaskFormValues): CreateTaskRequest {
  const optional = (value: string) => (value.trim() === '' ? null : value.trim())
  return {
    title: values.title.trim(),
    description: optional(values.description),
    priority: values.priority,
    assignedTo: optional(values.assignedTo),
    dueDateUtc: dateInputToUtcIso(values.dueDate),
  }
}
