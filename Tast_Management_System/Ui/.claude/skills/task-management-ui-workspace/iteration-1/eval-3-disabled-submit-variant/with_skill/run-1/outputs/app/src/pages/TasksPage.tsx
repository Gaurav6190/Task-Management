import { useAuth } from '../auth/useAuth'
import styles from './TasksPage.module.css'

/** Placeholder for the task list; protected by <RequireAuth>. */
export default function TasksPage() {
  const { session, logout } = useAuth()

  return (
    <div className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Tasks</h1>
        <div className={styles.account}>
          <span className={styles.user}>{session?.username}</span>
          <button type="button" className={styles.signOut} onClick={logout}>
            Sign out
          </button>
        </div>
      </header>
      <main className={styles.content}>
        <p>You are signed in. The task list will appear here.</p>
      </main>
    </div>
  )
}
