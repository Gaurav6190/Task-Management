import { useRef, useState, type FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router'
import {
  PASSWORD_MAX_LENGTH,
  USERNAME_MAX_LENGTH,
  sanitizeEmailInput,
  validatePassword,
  validateUsername,
} from '../auth/credentials'
import { describeLoginError, type LoginFailure } from '../auth/loginErrors'
import { useAuth } from '../auth/useAuth'
import styles from './LoginPage.module.css'

const NO_FAILURE: LoginFailure = { form: null, username: null, password: null }

export default function LoginPage() {
  const { session, login } = useAuth()
  const location = useLocation()
  const returnTo = (location.state as { from?: string } | null)?.from ?? '/tasks'

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [touched, setTouched] = useState({ username: false, password: false })
  const [attempted, setAttempted] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [failure, setFailure] = useState<LoginFailure>(NO_FAILURE)

  const usernameRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)

  // Signed in (now or after a successful submit): go where the user was heading.
  if (session) {
    return <Navigate to={returnTo} replace />
  }

  const usernameProblem = validateUsername(username)
  const passwordProblem = validatePassword(password)
  const usernameError = ((touched.username || attempted) && usernameProblem) || failure.username
  const passwordError = ((touched.password || attempted) && passwordProblem) || failure.password

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setAttempted(true)
    setFailure(NO_FAILURE)

    if (usernameProblem || passwordProblem) {
      ;(usernameProblem ? usernameRef : passwordRef).current?.focus()
      return
    }

    setSubmitting(true)
    try {
      await login({ username, password })
    } catch (error) {
      setFailure(describeLoginError(error))
      setSubmitting(false)
    }
  }

  return (
    <main className={styles.page}>
      <form className={styles.card} onSubmit={handleSubmit} noValidate aria-labelledby="login-title">
        <header className={styles.header}>
          <p className={styles.brand}>Task Management</p>
          <h1 id="login-title" className={styles.title}>
            Sign in
          </h1>
        </header>

        {failure.form && (
          <div className={styles.formError} role="alert">
            {failure.form}
          </div>
        )}

        <div className={styles.field}>
          <label htmlFor="username" className={styles.label}>
            Username
          </label>
          <input
            ref={usernameRef}
            id="username"
            name="username"
            type="email"
            inputMode="email"
            autoComplete="username"
            autoCapitalize="none"
            autoCorrect="off"
            spellCheck={false}
            maxLength={USERNAME_MAX_LENGTH}
            placeholder="name@example.com"
            className={styles.input}
            value={username}
            onChange={(e) => {
              setUsername(sanitizeEmailInput(e.target.value))
              setFailure((f) => ({ ...f, username: null }))
            }}
            onBlur={() => setTouched((t) => ({ ...t, username: true }))}
            aria-invalid={usernameError ? true : undefined}
            aria-describedby={usernameError ? 'username-hint username-error' : 'username-hint'}
            disabled={submitting}
            required
          />
          <p id="username-hint" className={styles.hint}>
            Use your email address.
          </p>
          {usernameError && (
            <p id="username-error" className={styles.fieldError}>
              {usernameError}
            </p>
          )}
        </div>

        <div className={styles.field}>
          <label htmlFor="password" className={styles.label}>
            Password
          </label>
          <div className={styles.passwordRow}>
            <input
              ref={passwordRef}
              id="password"
              name="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              maxLength={PASSWORD_MAX_LENGTH}
              className={styles.input}
              value={password}
              onChange={(e) => {
                setPassword(e.target.value)
                setFailure((f) => ({ ...f, password: null }))
              }}
              onBlur={() => setTouched((t) => ({ ...t, password: true }))}
              aria-invalid={passwordError ? true : undefined}
              aria-describedby={passwordError ? 'password-error' : undefined}
              disabled={submitting}
              required
            />
            <button
              type="button"
              className={styles.toggle}
              onClick={() => setShowPassword((s) => !s)}
              aria-pressed={showPassword}
              aria-controls="password"
              disabled={submitting}
            >
              {showPassword ? 'Hide' : 'Show'}
            </button>
          </div>
          {passwordError && (
            <p id="password-error" className={styles.fieldError}>
              {passwordError}
            </p>
          )}
        </div>

        <button type="submit" className={styles.submit} disabled={submitting}>
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  )
}
