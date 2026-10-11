import { PRIORITY_LABELS, TASK_PRIORITIES, isTaskPriority, type TaskPriority } from '../taskTypes'
import styles from './rowSelect.module.css'

interface PrioritySelectProps {
  /** Used for the accessible name: "Priority for {title}". */
  taskTitle: string
  value: TaskPriority
  onChange: (priority: TaskPriority) => void
  disabled?: boolean
  invalid?: boolean
  describedBy?: string
}

/**
 * Priority dropdown in a row's Priority cell. Changing it saves straight to the database (T4).
 * `data-priority` is the hook for the priority colours and icon added in T5.
 */
export function PrioritySelect({ taskTitle, value, onChange, disabled, invalid, describedBy }: PrioritySelectProps) {
  return (
    <select
      className={styles.select}
      data-priority={value}
      aria-label={`Priority for ${taskTitle}`}
      value={value}
      onChange={(e) => {
        if (isTaskPriority(e.target.value)) onChange(e.target.value)
      }}
      disabled={disabled}
      aria-invalid={invalid || undefined}
      aria-describedby={describedBy}
    >
      {TASK_PRIORITIES.map((priority) => (
        <option key={priority} value={priority}>
          {PRIORITY_LABELS[priority]}
        </option>
      ))}
    </select>
  )
}
