import { useAuth } from '../auth/useAuth'

export function TasksPage() {
  const { session, signOut } = useAuth()

  return (
    <div className="app-shell">
      <header className="topbar">
        <strong>Task Management</strong>
        <div className="topbar-right">
          <span className="muted">{session?.username}</span>
          <button type="button" className="secondary" onClick={signOut}>
            Log out
          </button>
        </div>
      </header>

      <main className="content">
        <h1>Tasks</h1>
        <p className="empty">No tasks yet.</p>
      </main>
    </div>
  )
}
