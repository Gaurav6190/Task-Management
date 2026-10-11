import { useState } from 'react'
import { useAuth } from '../auth/useAuth'
import { CreateTaskDialog } from '../features/tasks/create/CreateTaskDialog'
import { Pagination } from '../features/tasks/display/Pagination'
import { TaskTable } from '../features/tasks/display/TaskTable'
import { useTasks } from '../features/tasks/useTasks'
import styles from './TasksPage.module.css'

/** The task list: header, toolbar, table (filters + inline edits), pagination and the New task dialog. */
export default function TasksPage() {
  const { session, logout } = useAuth()
  const tasks = useTasks()
  const [creating, setCreating] = useState(false)
  const filtered = Boolean(tasks.filters.status || tasks.filters.priority)

  const newTaskButton = (
    <button type="button" className={styles.primary} onClick={() => setCreating(true)}>
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 5v14M5 12h14" />
      </svg>
      New task
    </button>
  )

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <p className={styles.brand}>Task Management</p>
        <div className={styles.account}>
          <span className={styles.user}>{session?.username}</span>
          <button type="button" className={styles.signOut} onClick={logout}>
            Sign out
          </button>
        </div>
      </header>

      <main className={styles.content}>
        <div className={styles.toolbar}>
          <div className={styles.heading}>
            <h1 className={styles.title}>Tasks</h1>
            {tasks.hasLoaded && (
              <span className={styles.count}>
                {tasks.totalCount} {tasks.totalCount === 1 ? 'task' : 'tasks'}
                {filtered && ' matching filters'}
              </span>
            )}
          </div>
          <div className={styles.actions}>
            {filtered && (
              <button type="button" className={styles.secondary} onClick={() => tasks.setFilters({})}>
                Clear filters
              </button>
            )}
            {newTaskButton}
          </div>
        </div>

        {tasks.notice && (
          <div className={styles.notice} role="status">
            <span>{tasks.notice}</span>
            <button type="button" className={styles.dismiss} onClick={tasks.dismissNotice} aria-label="Dismiss">
              ×
            </button>
          </div>
        )}

        <TaskTable
          tasks={tasks.tasks}
          loading={tasks.loading}
          error={tasks.error}
          hasLoaded={tasks.hasLoaded}
          onRetry={tasks.reload}
          filters={tasks.filters}
          onFiltersChange={tasks.setFilters}
          onUpdateTask={tasks.updateTaskFields}
          emptyAction={newTaskButton}
        />

        <Pagination
          page={tasks.page}
          pageSize={tasks.pageSize}
          totalPages={tasks.totalPages}
          totalCount={tasks.totalCount}
          onPageChange={tasks.setPage}
          disabled={tasks.loading}
        />
      </main>

      <CreateTaskDialog open={creating} onClose={() => setCreating(false)} onCreate={tasks.createTask} />
    </div>
  )
}
