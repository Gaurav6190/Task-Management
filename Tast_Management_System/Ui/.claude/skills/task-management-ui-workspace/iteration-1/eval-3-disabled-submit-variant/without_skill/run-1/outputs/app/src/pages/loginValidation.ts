// Mirrors LoginCommandValidator in TaskManagement.Application so the form
// only enables "Sign in" when the API would accept the shape of the request.
export const USERNAME_MAX_LENGTH = 256
export const PASSWORD_MAX_LENGTH = 128

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function validateUsername(value: string): string | null {
  const v = value.trim()
  if (!v) return 'Email is required.'
  if (v.length > USERNAME_MAX_LENGTH) return `Email cannot exceed ${USERNAME_MAX_LENGTH} characters.`
  if (!EMAIL_PATTERN.test(v)) return 'Enter a valid email address.'
  return null
}

export function validatePassword(value: string): string | null {
  if (!value) return 'Password is required.'
  if (value.length > PASSWORD_MAX_LENGTH) return `Password cannot exceed ${PASSWORD_MAX_LENGTH} characters.`
  return null
}
