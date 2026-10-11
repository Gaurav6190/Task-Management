import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { EMAIL_MAX_LENGTH, PASSWORD_MAX_LENGTH, isValidEmail, sanitizeEmailInput } from '../auth/email'
import { useAuth } from '../auth/useAuth'

export function LoginPage() {
  const { session, signIn } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [emailTouched, setEmailTouched] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (session) return <Navigate to="/tasks" replace />

  const emailValid = isValidEmail(email)
  const showEmailError = emailTouched && email.length > 0 && !emailValid
  const canSubmit = emailValid && password.length > 0 && !submitting

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setEmailTouched(true)
    if (!canSubmit) return

    setSubmitting(true)
    setError(null)
    try {
      await signIn(email, password)
      // On success the session is set and the <Navigate> above sends the user to /tasks.
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Login failed. Please try again.')
      setPassword('')
      setSubmitting(false)
    }
  }

  return (
    <main className="auth-page">
      <form className="card" onSubmit={handleSubmit} noValidate>
        <h1>Sign in</h1>
        <p className="muted">Task Management</p>

        <label htmlFor="email">Email</label>
        <input
          id="email"
          name="email"
          type="email"
          inputMode="email"
          autoComplete="username"
          placeholder="you@example.com"
          maxLength={EMAIL_MAX_LENGTH}
          value={email}
          onChange={(e) => setEmail(sanitizeEmailInput(e.target.value))}
          onBlur={() => setEmailTouched(true)}
          aria-invalid={showEmailError}
          aria-describedby={showEmailError ? 'email-error' : undefined}
          required
          autoFocus
        />
        {showEmailError && (
          <p id="email-error" className="field-error">
            Enter a valid email address, e.g. name@example.com.
          </p>
        )}

        <label htmlFor="password">Password</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          maxLength={PASSWORD_MAX_LENGTH}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" disabled={!canSubmit}>
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  )
}
