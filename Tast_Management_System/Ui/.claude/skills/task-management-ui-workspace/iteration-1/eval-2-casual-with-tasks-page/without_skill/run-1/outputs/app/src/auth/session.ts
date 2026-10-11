import type { LoginResponse } from '../api/auth'

export interface Session {
  accessToken: string
  username: string
  expiresAtUtc: string
}

// sessionStorage: the token survives a page refresh but is dropped when the tab closes.
const STORAGE_KEY = 'tm.session'

export function isExpired(session: Session): boolean {
  return Date.parse(session.expiresAtUtc) <= Date.now()
}

export function sessionFromLogin(response: LoginResponse): Session {
  return {
    accessToken: response.accessToken,
    username: response.username,
    expiresAtUtc: response.expiresAtUtc,
  }
}

export function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    if (!session.accessToken || !session.expiresAtUtc || isExpired(session)) {
      sessionStorage.removeItem(STORAGE_KEY)
      return null
    }
    return session
  } catch {
    return null
  }
}

export function saveSession(session: Session): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
  } catch {
    // Storage unavailable (e.g. private mode): the session just won't survive a refresh.
  }
}

export function clearSession(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // ignore
  }
}
