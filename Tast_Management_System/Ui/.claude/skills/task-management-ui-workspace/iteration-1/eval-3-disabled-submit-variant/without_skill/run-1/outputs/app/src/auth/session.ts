import type { LoginResponse } from '../api/auth'

// sessionStorage: the token is gone when the tab closes, and it expires after 60 minutes anyway.
const STORAGE_KEY = 'tms.session'

export interface Session {
  accessToken: string
  tokenType: string
  expiresAtUtc: string
  username: string
}

export function saveSession(login: LoginResponse): Session {
  const session: Session = { ...login }
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
  } catch {
    // Storage unavailable (private mode etc.): the session lives in memory only.
  }
  return session
}

export function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    if (isExpired(session)) {
      clearSession()
      return null
    }
    return session
  } catch {
    return null
  }
}

export function clearSession(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // ignore
  }
}

export function isExpired(session: Session): boolean {
  const expires = Date.parse(session.expiresAtUtc)
  return Number.isNaN(expires) || expires <= Date.now()
}

/** Value for the Authorization header on task API calls. */
export function authorizationHeader(session: Session): string {
  return `${session.tokenType} ${session.accessToken}`
}
