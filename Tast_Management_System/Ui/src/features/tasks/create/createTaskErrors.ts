import { ApiError } from '../../../api/httpClient'
import { describeTaskError } from '../taskErrors'
import type { TaskFormErrors, TaskFormField } from './taskFormValidation'

// The API names fields C#-style in `errors` (problem details); map them onto the form's fields.
const API_FIELD_TO_FORM: Record<string, TaskFormField> = {
  title: 'title',
  description: 'description',
  priority: 'priority',
  assignedto: 'assignedTo',
  duedateutc: 'dueDate',
}

export interface CreateTaskFailure {
  /** Shown above the form. */
  form: string | null
  fields: TaskFormErrors
}

/** Turns anything thrown by createTask into field errors and/or one form-level message. */
export function describeCreateError(error: unknown): CreateTaskFailure {
  if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
    const fields: TaskFormErrors = {}
    const unmapped: string[] = []

    for (const [key, messages] of Object.entries(error.problem.errors)) {
      const field = API_FIELD_TO_FORM[key.toLowerCase()]
      if (field && messages[0]) fields[field] = messages[0]
      else unmapped.push(...messages)
    }

    const form = unmapped.length > 0 ? unmapped.join(' ') : Object.keys(fields).length === 0 ? describeTaskError(error) : null
    return { form, fields }
  }

  return { form: describeTaskError(error), fields: {} }
}
