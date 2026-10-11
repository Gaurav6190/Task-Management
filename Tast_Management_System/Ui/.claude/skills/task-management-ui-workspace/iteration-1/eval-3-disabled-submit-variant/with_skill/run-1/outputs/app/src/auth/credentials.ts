// Limits mirror TaskManagement.Application LoginCommandValidator, so the UI rejects
// exactly what the API would reject, before a request is sent.
export const USERNAME_MAX_LENGTH = 256
export const PASSWORD_MAX_LENGTH = 128

// local@domain.tld: no whitespace, a dotted domain, and a 2+ letter top-level domain.
// Stricter than the API's check on purpose: "a@b" passes the server but is never a real login.
const EMAIL_PATTERN =
  /^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)*\.[A-Za-z]{2,}$/

/** Email addresses never contain whitespace, so drop it as the user types or pastes. */
export function sanitizeEmailInput(value: string): string {
  return value.replace(/\s+/g, '')
}

export function isValidEmail(value: string): boolean {
  return value.length <= USERNAME_MAX_LENGTH && EMAIL_PATTERN.test(value)
}

/** Returns an error message, or null when the username is a valid email address. */
export function validateUsername(value: string): string | null {
  if (value.length === 0) return 'Email address is required.'
  if (value.length > USERNAME_MAX_LENGTH) return `Email address cannot exceed ${USERNAME_MAX_LENGTH} characters.`
  if (!EMAIL_PATTERN.test(value)) return 'Enter a valid email address, like name@example.com.'
  return null
}

/** Returns an error message, or null when the password is acceptable. */
export function validatePassword(value: string): string | null {
  if (value.length === 0) return 'Password is required.'
  if (value.length > PASSWORD_MAX_LENGTH) return `Password cannot exceed ${PASSWORD_MAX_LENGTH} characters.`
  return null
}
