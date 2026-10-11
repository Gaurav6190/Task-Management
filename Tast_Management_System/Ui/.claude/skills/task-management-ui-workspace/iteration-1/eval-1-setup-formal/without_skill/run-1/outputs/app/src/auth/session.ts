import type { LoginResponse } from '../api/auth'

/**
 * The access token lives in sessionStorage: it survives a page reload but not closing the tab.
 * The API issues a 60-minute JWT with no refresh token, so an expired session simply means "log in again".
 */
const STORAGE_KEY = 'taskManagement.session'

export interface Session {
  accessToken: string
  tokenType: string
  expiresAtUtc: string
  username: string
}

export function isSessionValid(session: Session | null): session is Session {
  return session !== null && Date.parse(session.expiresAtUtc) > Date.now()
}

export function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    const session = raw ? (JSON.parse(raw) as Session) : null
    return isSessionValid(session) ? session : null
  } catch {
    return null
  }
}

export function saveSession(response: LoginResponse): Session {
  const session: Session = { ...response }
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
  } catch {
    // Storage unavailable (e.g. private mode): the session still works for this page load.
  }
  return session
}

export function clearSession(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // ignore
  }
}
