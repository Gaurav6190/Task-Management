import { PASSWORD_MAX_LENGTH, USERNAME_MAX_LENGTH } from '../api/auth'

/** One "@", something before it, and a dotted domain after it, no spaces. */
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export interface LoginFieldErrors {
  username?: string
  password?: string
}

export function validateUsername(value: string): string | undefined {
  const username = value.trim()
  if (!username) return 'Email is required.'
  if (username.length > USERNAME_MAX_LENGTH) return `Email cannot exceed ${USERNAME_MAX_LENGTH} characters.`
  if (!EMAIL_PATTERN.test(username)) return 'Enter a valid email address, e.g. name@example.com.'
  return undefined
}

export function validatePassword(value: string): string | undefined {
  if (!value) return 'Password is required.'
  if (value.length > PASSWORD_MAX_LENGTH) return `Password cannot exceed ${PASSWORD_MAX_LENGTH} characters.`
  return undefined
}

export function validateLogin(username: string, password: string): LoginFieldErrors {
  const errors: LoginFieldErrors = {}
  const usernameError = validateUsername(username)
  const passwordError = validatePassword(password)
  if (usernameError) errors.username = usernameError
  if (passwordError) errors.password = passwordError
  return errors
}
