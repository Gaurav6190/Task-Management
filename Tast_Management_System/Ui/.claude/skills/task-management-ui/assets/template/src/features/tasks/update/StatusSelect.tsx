import { STATUS_LABELS, TASK_STATUSES, isTaskStatus, type TaskStatus } from '../taskTypes'
import styles from './rowSelect.module.css'

interface StatusSelectProps {
  /** Used for the accessible name: "Status for {title}". */
  taskTitle: string
  value: TaskStatus
  onChange: (status: TaskStatus) => void
  disabled?: boolean
  invalid?: boolean
  describedBy?: string
}

/** Status dropdown in a row's Status cell. Changing it saves straight to the database (T4). */
export function StatusSelect({ taskTitle, value, onChange, disabled, invalid, describedBy }: StatusSelectProps) {
  return (
    <select
      className={styles.select}
      data-status={value}
      aria-label={`Status for ${taskTitle}`}
      value={value}
      onChange={(e) => {
        if (isTaskStatus(e.target.value)) onChange(e.target.value)
      }}
      disabled={disabled}
      aria-invalid={invalid || undefined}
      aria-describedby={describedBy}
    >
      {TASK_STATUSES.map((status) => (
        <option key={status} value={status}>
          {STATUS_LABELS[status]}
        </option>
      ))}
    </select>
  )
}
