/** The API requires the username to be an email address of at most 256 characters. */
export const EMAIL_MAX_LENGTH = 256
export const PASSWORD_MAX_LENGTH = 128

// local@domain.tld, no whitespace, one "@", a dot in the domain.
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function isValidEmail(value: string): boolean {
  return value.length <= EMAIL_MAX_LENGTH && EMAIL_PATTERN.test(value)
}

/** Emails never contain whitespace, so strip it as the user types or pastes. */
export function sanitizeEmailInput(value: string): string {
  return value.replace(/\s+/g, '')
}
