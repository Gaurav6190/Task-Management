import { useState, type FormEvent } from 'react'
import { login, PASSWORD_MAX_LENGTH, USERNAME_MAX_LENGTH } from '../api/auth'
import { ApiError } from '../api/client'
import { saveSession, type Session } from '../auth/session'
import { validateLogin, validatePassword, validateUsername, type LoginFieldErrors } from '../auth/validation'
import './LoginPage.css'

interface LoginPageProps {
  onLoggedIn: (session: Session) => void
}

/** Maps the API's validation errors ({ "Username": [...], "Password": [...] }) onto the form fields. */
function fieldErrorsFrom(error: ApiError): LoginFieldErrors {
  const errors = error.problem?.errors ?? {}
  return {
    username: errors.Username?.[0],
    password: errors.Password?.[0],
  }
}

export default function LoginPage({ onLoggedIn }: LoginPageProps) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [fieldErrors, setFieldErrors] = useState<LoginFieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting) return

    const errors = validateLogin(username, password)
    setFieldErrors(errors)
    setFormError(null)
    if (errors.username || errors.password) return

    setSubmitting(true)
    try {
      const response = await login({ username: username.trim(), password })
      onLoggedIn(saveSession(response))
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.status === 400 && error.problem?.errors) {
          setFieldErrors(fieldErrorsFrom(error))
        } else if (error.status === 401) {
          // Same message for unknown email and wrong password (the API never reveals which).
          setFormError('Invalid email or password.')
        } else {
          setFormError(error.message)
        }
      } else {
        setFormError('Something went wrong. Please try again.')
      }
      setPassword('')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="login">
      <form className="login__card" onSubmit={handleSubmit} noValidate aria-busy={submitting}>
        <h1 className="login__title">Task Management</h1>
        <p className="login__subtitle">Sign in to continue</p>

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
            placeholder="name@example.com"
            maxLength={USERNAME_MAX_LENGTH}
            value={username}
            onChange={(e) => {
              setUsername(e.target.value)
              if (fieldErrors.username) setFieldErrors((prev) => ({ ...prev, username: undefined }))
            }}
            onBlur={() => {
              if (username) setFieldErrors((prev) => ({ ...prev, username: validateUsername(username) }))
            }}
            aria-invalid={fieldErrors.username ? true : undefined}
            aria-describedby={fieldErrors.username ? 'username-error' : undefined}
            disabled={submitting}
            autoFocus
            required
          />
          {fieldErrors.username && (
            <p id="username-error" className="login__error">
              {fieldErrors.username}
            </p>
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
              if (fieldErrors.password) setFieldErrors((prev) => ({ ...prev, password: undefined }))
            }}
            onBlur={() => {
              if (password) setFieldErrors((prev) => ({ ...prev, password: validatePassword(password) }))
            }}
            aria-invalid={fieldErrors.password ? true : undefined}
            aria-describedby={fieldErrors.password ? 'password-error' : undefined}
            disabled={submitting}
            required
          />
          {fieldErrors.password && (
            <p id="password-error" className="login__error">
              {fieldErrors.password}
            </p>
          )}
        </div>

        <button className="login__submit" type="submit" disabled={submitting}>
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </main>
  )
}
