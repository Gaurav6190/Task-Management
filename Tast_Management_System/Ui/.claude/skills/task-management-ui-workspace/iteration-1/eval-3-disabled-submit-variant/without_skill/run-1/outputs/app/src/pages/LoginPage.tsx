import { useEffect, useRef, useState, type FormEvent } from 'react'
import { ApiError, login } from '../api/auth'
import { saveSession, type Session } from '../auth/session'
import {
  PASSWORD_MAX_LENGTH,
  USERNAME_MAX_LENGTH,
  validatePassword,
  validateUsername,
} from './loginValidation'
import './LoginPage.css'

interface LoginPageProps {
  onSignedIn: (session: Session) => void
}

export default function LoginPage({ onSignedIn }: LoginPageProps) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [touched, setTouched] = useState({ username: false, password: false })
  const [serverFieldErrors, setServerFieldErrors] = useState<Record<string, string[]>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const abortRef = useRef<AbortController | null>(null)

  useEffect(() => () => abortRef.current?.abort(), [])

  const usernameError = validateUsername(username)
  const passwordError = validatePassword(password)
  const isValid = usernameError === null && passwordError === null
  // Requirement: Sign in stays disabled until both fields are valid (and while a request is in flight).
  const canSubmit = isValid && !submitting

  const shownUsernameError =
    (touched.username ? usernameError : null) ?? serverFieldErrors.username?.[0] ?? null
  const shownPasswordError =
    (touched.password ? passwordError : null) ?? serverFieldErrors.password?.[0] ?? null

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!canSubmit) return

    setSubmitting(true)
    setFormError(null)
    setServerFieldErrors({})
    abortRef.current?.abort()
    const controller = new AbortController()
    abortRef.current = controller

    try {
      const result = await login({ username: username.trim(), password }, controller.signal)
      onSignedIn(saveSession(result))
    } catch (err) {
      if (controller.signal.aborted) return
      if (err instanceof ApiError) {
        setServerFieldErrors(err.fieldErrors)
        setFormError(err.message)
        if (err.status === 401) setPassword('')
      } else {
        setFormError('Something went wrong. Please try again.')
      }
    } finally {
      if (!controller.signal.aborted) setSubmitting(false)
    }
  }

  return (
    <main className="login">
      <form className="login__card" onSubmit={handleSubmit} noValidate aria-busy={submitting}>
        <h1 className="login__title">Task Management</h1>
        <p className="login__subtitle">Sign in with your email address</p>

        {formError && (
          <div className="login__alert" role="alert">
            {formError}
          </div>
        )}

        <div className="login__field">
          <label htmlFor="username">Email</label>
          <input
            id="username"
            name="username"
            type="email"
            inputMode="email"
            autoComplete="username"
            autoFocus
            maxLength={USERNAME_MAX_LENGTH}
            value={username}
            onChange={(e) => {
              setUsername(e.target.value)
              setServerFieldErrors((s) => ({ ...s, username: [] }))
            }}
            onBlur={() => setTouched((t) => ({ ...t, username: true }))}
            aria-invalid={shownUsernameError ? true : undefined}
            aria-describedby={shownUsernameError ? 'username-error' : undefined}
            disabled={submitting}
            required
          />
          {shownUsernameError && (
            <span id="username-error" className="login__error">
              {shownUsernameError}
            </span>
          )}
        </div>

        <div className="login__field">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            maxLength={PASSWORD_MAX_LENGTH}
            value={password}
            onChange={(e) => {
              setPassword(e.target.value)
              setServerFieldErrors((s) => ({ ...s, password: [] }))
            }}
            onBlur={() => setTouched((t) => ({ ...t, password: true }))}
            aria-invalid={shownPasswordError ? true : undefined}
            aria-describedby={shownPasswordError ? 'password-error' : undefined}
            disabled={submitting}
            required
          />
          {shownPasswordError && (
            <span id="password-error" className="login__error">
              {shownPasswordError}
            </span>
          )}
        </div>

        <button type="submit" className="login__submit" disabled={!canSubmit}>
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  )
}
