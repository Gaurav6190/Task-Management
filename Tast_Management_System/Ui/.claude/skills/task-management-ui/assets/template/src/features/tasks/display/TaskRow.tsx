import { formatLocalDate, formatUtcDate, isPastUtcDate } from '../../../lib/dates'
import { PriorityIcon } from '../priority/PriorityIcon'
import type { TaskDto } from '../taskTypes'
import { PrioritySelect } from '../update/PrioritySelect'
import { SaveIndicator } from '../update/SaveIndicator'
import { StatusSelect } from '../update/StatusSelect'
import { useRowUpdate, type EditableField } from '../update/useRowUpdate'
import type { TaskFieldChanges } from '../useTasks'
import styles from './TaskRow.module.css'

interface TaskRowProps {
  task: TaskDto
  /** useTasks.updateTaskFields: saves to the database; resolves to null or an error message. */
  onUpdate: (task: TaskDto, changes: TaskFieldChanges) => Promise<string | null>
  /** The row was edited so it no longer matches the active filters; it goes away on the next refresh. */
  hiddenByFilter: boolean
}

/** One task as a table row, with inline Status and Priority dropdowns (T4). */
export function TaskRow({ task, onUpdate, hiddenByFilter }: TaskRowProps) {
  const row = useRowUpdate(task, onUpdate)
  const done = task.status === 'Done'
  const overdue = !done && task.dueDateUtc !== null && isPastUtcDate(task.dueDateUtc)
  const errorId = (field: EditableField) => `task-${task.id}-${field}-error`
  const indicator = (field: EditableField) =>
    row.saving === field ? 'saving' : row.saved === field ? 'saved' : null

  return (
    <tr className={done ? `${styles.row} ${styles.done}` : styles.row} data-priority={task.priority}>
      <th scope="row" className={`${styles.cell} ${styles.titleCell}`}>
        <span className={styles.title} title={task.title}>
          {task.title}
        </span>
        {task.description && (
          <span className={styles.description} title={task.description}>
            {task.description}
          </span>
        )}
        {hiddenByFilter && <span className={styles.filterNote}>Hidden by the current filter after refresh</span>}
      </th>

      <td className={styles.cell}>
        <div className={styles.editCell}>
          <StatusSelect
            taskTitle={task.title}
            value={task.status}
            onChange={row.changeStatus}
            disabled={row.saving !== null}
            invalid={row.error?.field === 'status'}
            describedBy={row.error?.field === 'status' ? errorId('status') : undefined}
          />
          <SaveIndicator state={indicator('status')} label="Status" />
        </div>
        {row.error?.field === 'status' && (
          <p id={errorId('status')} className={styles.cellError} role="alert">
            {row.error.message}
          </p>
        )}
      </td>

      <td className={styles.cell}>
        <div className={styles.editCell}>
          <PriorityIcon priority={task.priority} />
          <PrioritySelect
            taskTitle={task.title}
            value={task.priority}
            onChange={row.changePriority}
            disabled={row.saving !== null}
            invalid={row.error?.field === 'priority'}
            describedBy={row.error?.field === 'priority' ? errorId('priority') : undefined}
          />
          <SaveIndicator state={indicator('priority')} label="Priority" />
        </div>
        {row.error?.field === 'priority' && (
          <p id={errorId('priority')} className={styles.cellError} role="alert">
            {row.error.message}
          </p>
        )}
      </td>

      <td className={`${styles.cell} ${styles.truncate}`} title={task.assignedTo ?? undefined}>
        {task.assignedTo ?? <span className={styles.muted}>Unassigned</span>}
      </td>

      <td className={`${styles.cell} ${styles.date}`}>
        {done && task.completedAtUtc ? (
          <span className={styles.muted}>Completed {formatLocalDate(task.completedAtUtc)}</span>
        ) : task.dueDateUtc ? (
          <>
            {formatUtcDate(task.dueDateUtc)}
            {overdue && <span className={styles.overdue}>Overdue</span>}
          </>
        ) : (
          <span className={styles.muted} aria-label="No due date">
            —
          </span>
        )}
      </td>
    </tr>
  )
}
