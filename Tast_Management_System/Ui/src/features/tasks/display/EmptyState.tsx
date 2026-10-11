import type { ReactNode } from 'react'
import styles from './EmptyState.module.css'

/**
 * Shown instead of the table when the user has no tasks at all.
 * (When filters hide everything, TaskTable keeps its header and shows a "no matches" row instead.)
 */
export function EmptyState({ action }: { action?: ReactNode }) {
  return (
    <div className={styles.empty}>
      <svg className={styles.icon} viewBox="0 0 24 24" aria-hidden="true">
        <path d="M9 11l3 3 8-8" />
        <path d="M20 12v6a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h9" />
      </svg>
      <h2 className={styles.title}>No tasks yet</h2>
      <p className={styles.text}>Create your first task to get started.</p>
      {action}
    </div>
  )
}
