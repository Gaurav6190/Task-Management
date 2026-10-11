import type { ReactNode } from 'react'
import { PRIORITY_LABELS, STATUS_LABELS, TASK_PRIORITIES, TASK_STATUSES, type TaskDto } from '../taskTypes'
import type { TaskFieldChanges, TaskFilters } from '../useTasks'
import { ColumnFilter } from './ColumnFilter'
import { EmptyState } from './EmptyState'
import { TaskRow } from './TaskRow'
import styles from './TaskTable.module.css'

const COLUMN_COUNT = 5
const SKELETON_ROWS = 5
const STATUS_OPTIONS = TASK_STATUSES.map((value) => ({ value, label: STATUS_LABELS[value] }))
const PRIORITY_OPTIONS = TASK_PRIORITIES.map((value) => ({ value, label: PRIORITY_LABELS[value] }))

interface TaskTableProps {
  tasks: TaskDto[]
  loading: boolean
  error: string | null
  hasLoaded: boolean
  onRetry: () => void
  filters: TaskFilters
  onFiltersChange: (next: TaskFilters) => void
  /** Saves an inline status/priority change (T4); resolves to null or an error message. */
  onUpdateTask: (task: TaskDto, changes: TaskFieldChanges) => Promise<string | null>
  /** Shown in the empty state when there are no tasks at all. */
  emptyAction?: ReactNode
}

/** Tasks as a table, with Status and Priority filters in the column headers (D9). */
export function TaskTable({
  tasks,
  loading,
  error,
  hasLoaded,
  onRetry,
  filters,
  onFiltersChange,
  onUpdateTask,
  emptyAction,
}: TaskTableProps) {
  const filtered = Boolean(filters.status || filters.priority)
  const clearFilters = () => onFiltersChange({})

  const errorBanner = error && (
    <div className={styles.error} role="alert">
      <span>{error}</span>
      <button type="button" className={styles.retry} onClick={onRetry}>
        Retry
      </button>
    </div>
  )

  // First load failed: there is nothing to put in a table yet.
  if (!hasLoaded && error) return errorBanner

  // No tasks at all (not just hidden by filters): invite the user to create one.
  if (hasLoaded && !filtered && tasks.length === 0 && !error) {
    return <EmptyState action={emptyAction} />
  }

  return (
    <div className={styles.wrapper}>
      {errorBanner}

      <div className={styles.panel}>
        <div className={styles.scroll}>
          <table className={loading && hasLoaded ? `${styles.table} ${styles.stale}` : styles.table} aria-busy={loading}>
            <caption className={styles.srOnly}>Tasks</caption>
            <colgroup>
              <col />
              <col className={styles.colStatus} />
              <col className={styles.colPriority} />
              <col className={styles.colAssignee} />
              <col className={styles.colDue} />
            </colgroup>
            <thead>
              <tr>
                <th scope="col" className={`${styles.th} ${styles.thTitle}`}>
                  Title
                </th>
                <th scope="col" className={styles.th}>
                  <span className={styles.thLabel}>Status</span>
                  <ColumnFilter
                    label="Filter by status"
                    value={filters.status}
                    options={STATUS_OPTIONS}
                    onChange={(status) => onFiltersChange({ ...filters, status })}
                  />
                </th>
                <th scope="col" className={styles.th}>
                  <span className={styles.thLabel}>Priority</span>
                  <ColumnFilter
                    label="Filter by priority"
                    value={filters.priority}
                    options={PRIORITY_OPTIONS}
                    onChange={(priority) => onFiltersChange({ ...filters, priority })}
                  />
                </th>
                <th scope="col" className={styles.th}>
                  Assigned to
                </th>
                <th scope="col" className={styles.th}>
                  Due date
                </th>
              </tr>
            </thead>
            <tbody>
              {!hasLoaded && loading && <SkeletonRows />}

              {hasLoaded && filtered && tasks.length === 0 && !loading && (
                <tr>
                  <td colSpan={COLUMN_COUNT} className={styles.noMatch}>
                    <p className={styles.noMatchText}>No tasks match these filters.</p>
                    <button type="button" className={styles.clear} onClick={clearFilters}>
                      Clear filters
                    </button>
                  </td>
                </tr>
              )}

              {tasks.map((task) => (
                <TaskRow
                  key={task.id}
                  task={task}
                  onUpdate={onUpdateTask}
                  hiddenByFilter={
                    Boolean(filters.status && task.status !== filters.status) ||
                    Boolean(filters.priority && task.priority !== filters.priority)
                  }
                />
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}

function SkeletonRows() {
  return Array.from({ length: SKELETON_ROWS }, (_, row) => (
    <tr key={row} aria-hidden="true">
      {Array.from({ length: COLUMN_COUNT }, (_, col) => (
        <td key={col} className={styles.skeletonCell}>
          <span className={styles.skeletonLine} style={{ width: col === 0 ? '80%' : '60%' }} />
        </td>
      ))}
    </tr>
  ))
}
