import { useEffect, useState } from 'react'
import { clearSession, isExpired, loadSession, type Session } from './auth/session'
import LoginPage from './pages/LoginPage'
import './App.css'

export default function App() {
  const [session, setSession] = useState<Session | null>(() => loadSession())

  // Drop back to the login page when the 60-minute token expires.
  useEffect(() => {
    if (!session) return
    const ms = Date.parse(session.expiresAtUtc) - Date.now()
    const timer = setTimeout(() => {
      clearSession()
      setSession(null)
    }, Math.max(0, ms))
    return () => clearTimeout(timer)
  }, [session])

  if (!session || isExpired(session)) {
    return <LoginPage onSignedIn={setSession} />
  }

  return (
    <main className="home">
      <header className="home__header">
        <h1>Task Management</h1>
        <div className="home__user">
          <span>{session.username}</span>
          <button
            type="button"
            onClick={() => {
              clearSession()
              setSession(null)
            }}
          >
            Sign out
          </button>
        </div>
      </header>
      <p>You are signed in. Task pages come next.</p>
    </main>
  )
}
