import type { LoginResponse } from '../api/authApi'

export interface Session {
  accessToken: string
  username: string
  /** Epoch milliseconds. */
  expiresAt: number
}

// sessionStorage: survives a page refresh, cleared when the tab closes, never shared across tabs.
const STORAGE_KEY = 'taskManagement.session'

export function sessionFromLogin(response: LoginResponse): Session {
  return {
    accessToken: response.accessToken,
    username: response.username,
    expiresAt: parseUtc(response.expiresAtUtc),
  }
}

export function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Partial<Session>
    if (
      typeof session.accessToken !== 'string' ||
      typeof session.username !== 'string' ||
      typeof session.expiresAt !== 'number' ||
      session.expiresAt <= Date.now()
    ) {
      clearSession()
      return null
    }
    return session as Session
  } catch {
    return null
  }
}

export function saveSession(session: Session): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session))
  } catch {
    // Storage unavailable (private mode, quota): the session still lives in memory.
  }
}

export function clearSession(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // Nothing to clear.
  }
}

/** .NET may omit the "Z" on UTC DateTimes; without it Date.parse would read the value as local time. */
function parseUtc(value: string): number {
  const hasZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value)
  return Date.parse(hasZone ? value : `${value}Z`)
}
