import { useId, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { todayUtcDateInput } from '../../../lib/dates'
import { PRIORITY_LABELS, TASK_LIMITS, TASK_PRIORITIES, isTaskPriority, type CreateTaskRequest } from '../taskTypes'
import { describeCreateError } from './createTaskErrors'
import {
  EMPTY_TASK_FORM,
  hasErrors,
  toCreateRequest,
  validateTaskForm,
  type TaskFormErrors,
  type TaskFormField,
  type TaskFormValues,
} from './taskFormValidation'
import styles from './CreateTaskForm.module.css'

// Fields that can fail client-side validation, in the order they appear (for focusing the first error).
const FOCUS_ORDER: TaskFormField[] = ['title', 'description', 'assignedTo', 'dueDate']

interface CreateTaskFormProps {
  /** Sends the request. Rejects with the API error if it fails; the form keeps the user's input. */
  onSubmit: (request: CreateTaskRequest) => Promise<void>
  onCancel: () => void
}

export function CreateTaskForm({ onSubmit, onCancel }: CreateTaskFormProps) {
  const idPrefix = useId()
  const id = (field: TaskFormField) => `${idPrefix}-${field}`

  const [values, setValues] = useState<TaskFormValues>(EMPTY_TASK_FORM)
  const [touched, setTouched] = useState<Partial<Record<TaskFormField, boolean>>>({})
  const [attempted, setAttempted] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [serverErrors, setServerErrors] = useState<TaskFormErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const fieldRefs = useRef<Partial<Record<TaskFormField, HTMLElement | null>>>({})

  const today = todayUtcDateInput()
  const clientErrors = validateTaskForm(values, today)

  // Client errors appear once a field has been left or after a submit attempt; server errors until edited.
  const errorFor = (field: TaskFormField) =>
    ((touched[field] || attempted) && clientErrors[field]) || serverErrors[field] || undefined

  function update<K extends TaskFormField>(field: K, value: TaskFormValues[K]) {
    setValues((current) => ({ ...current, [field]: value }))
    setServerErrors((current) => ({ ...current, [field]: undefined }))
  }

  const blur = (field: TaskFormField) => () => setTouched((current) => ({ ...current, [field]: true }))

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting) return
    setAttempted(true)
    setFormError(null)

    if (hasErrors(clientErrors)) {
      const first = FOCUS_ORDER.find((field) => clientErrors[field])
      if (first) fieldRefs.current[first]?.focus()
      return
    }

    setSubmitting(true)
    try {
      await onSubmit(toCreateRequest(values))
      // Success: the dialog closes and unmounts this form.
    } catch (error) {
      const failure = describeCreateError(error)
      setServerErrors(failure.fields)
      setFormError(failure.form)
      setSubmitting(false)
    }
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      {formError && (
        <div className={styles.formError} role="alert">
          {formError}
        </div>
      )}

      <Field id={id('title')} label="Title" required error={errorFor('title')}>
        <input
          ref={(el) => {
            fieldRefs.current.title = el
          }}
          id={id('title')}
          className={styles.input}
          value={values.title}
          maxLength={TASK_LIMITS.titleMaxLength}
          onChange={(e) => update('title', e.target.value)}
          onBlur={blur('title')}
          aria-invalid={errorFor('title') ? true : undefined}
          aria-describedby={errorFor('title') ? `${id('title')}-error` : undefined}
          aria-required="true"
          disabled={submitting}
          autoFocus
        />
      </Field>

      <Field
        id={id('description')}
        label="Description"
        error={errorFor('description')}
        hint={`${values.description.length} / ${TASK_LIMITS.descriptionMaxLength}`}
      >
        <textarea
          ref={(el) => {
            fieldRefs.current.description = el
          }}
          id={id('description')}
          className={`${styles.input} ${styles.textarea}`}
          rows={4}
          value={values.description}
          maxLength={TASK_LIMITS.descriptionMaxLength}
          onChange={(e) => update('description', e.target.value)}
          onBlur={blur('description')}
          aria-invalid={errorFor('description') ? true : undefined}
          aria-describedby={`${id('description')}-hint${errorFor('description') ? ` ${id('description')}-error` : ''}`}
          disabled={submitting}
        />
      </Field>

      <div className={styles.row}>
        <Field id={id('priority')} label="Priority" error={errorFor('priority')}>
          <select
            id={id('priority')}
            className={styles.input}
            value={values.priority}
            onChange={(e) => {
              if (isTaskPriority(e.target.value)) update('priority', e.target.value)
            }}
            aria-invalid={errorFor('priority') ? true : undefined}
            aria-describedby={errorFor('priority') ? `${id('priority')}-error` : undefined}
            disabled={submitting}
          >
            {TASK_PRIORITIES.map((priority) => (
              <option key={priority} value={priority}>
                {PRIORITY_LABELS[priority]}
              </option>
            ))}
          </select>
        </Field>

        <Field id={id('dueDate')} label="Due date" error={errorFor('dueDate')}>
          <input
            ref={(el) => {
              fieldRefs.current.dueDate = el
            }}
            id={id('dueDate')}
            type="date"
            className={styles.input}
            min={today}
            value={values.dueDate}
            onChange={(e) => update('dueDate', e.target.value)}
            onBlur={blur('dueDate')}
            aria-invalid={errorFor('dueDate') ? true : undefined}
            aria-describedby={errorFor('dueDate') ? `${id('dueDate')}-error` : undefined}
            disabled={submitting}
          />
        </Field>
      </div>

      <Field id={id('assignedTo')} label="Assigned to" error={errorFor('assignedTo')}>
        <input
          ref={(el) => {
            fieldRefs.current.assignedTo = el
          }}
          id={id('assignedTo')}
          className={styles.input}
          value={values.assignedTo}
          maxLength={TASK_LIMITS.assignedToMaxLength}
          placeholder="Name or email (optional)"
          onChange={(e) => update('assignedTo', e.target.value)}
          onBlur={blur('assignedTo')}
          aria-invalid={errorFor('assignedTo') ? true : undefined}
          aria-describedby={errorFor('assignedTo') ? `${id('assignedTo')}-error` : undefined}
          disabled={submitting}
        />
      </Field>

      <div className={styles.footer}>
        <button type="button" className={styles.secondary} onClick={onCancel} disabled={submitting}>
          Cancel
        </button>
        <button type="submit" className={styles.primary} disabled={submitting}>
          {submitting ? 'Creating…' : 'Create task'}
        </button>
      </div>
    </form>
  )
}

interface FieldProps {
  id: string
  label: string
  required?: boolean
  error?: string
  hint?: string
  children: ReactNode
}

function Field({ id, label, required = false, error, hint, children }: FieldProps) {
  return (
    <div className={styles.field}>
      <div className={styles.labelRow}>
        <label htmlFor={id} className={styles.label}>
          {label}
          {required && (
            <span className={styles.required} aria-hidden="true">
              {' '}
              *
            </span>
          )}
        </label>
        {hint && (
          <span id={`${id}-hint`} className={styles.hint}>
            {hint}
          </span>
        )}
      </div>
      {children}
      {error && (
        <p id={`${id}-error`} className={styles.fieldError}>
          {error}
        </p>
      )}
    </div>
  )
}
