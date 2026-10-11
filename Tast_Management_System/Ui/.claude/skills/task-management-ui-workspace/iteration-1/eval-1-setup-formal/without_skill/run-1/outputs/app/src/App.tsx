import { useState } from 'react'
import { clearSession, loadSession, type Session } from './auth/session'
import LoginPage from './pages/LoginPage'
import './App.css'

export default function App() {
  const [session, setSession] = useState<Session | null>(() => loadSession())

  if (!session) {
    return <LoginPage onLoggedIn={setSession} />
  }

  // Placeholder until the task pages are built.
  return (
    <main className="home">
      <p>
        Signed in as <strong>{session.username}</strong>.
      </p>
      <button
        type="button"
        onClick={() => {
          clearSession()
          setSession(null)
        }}
      >
        Sign out
      </button>
    </main>
  )
}
