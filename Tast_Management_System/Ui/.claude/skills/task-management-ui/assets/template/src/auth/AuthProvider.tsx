import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import * as authApi from '../api/authApi'
import { AuthContext, type AuthContextValue } from './AuthContext'
import { clearSession, loadSession, saveSession, sessionFromLogin, type Session } from './session'

// setTimeout overflows above 2^31-1 ms (~24.8 days).
const MAX_TIMEOUT_MS = 2_147_483_647

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(loadSession)

  const login = useCallback(async (credentials: authApi.LoginRequest) => {
    const response = await authApi.login(credentials)
    const next = sessionFromLogin(response)
    saveSession(next)
    setSession(next)
  }, [])

  const logout = useCallback(() => {
    clearSession()
    setSession(null)
  }, [])

  // Sign out the moment the token expires instead of waiting for the next 401.
  useEffect(() => {
    if (!session) return
    const delay = Math.min(Math.max(session.expiresAt - Date.now(), 0), MAX_TIMEOUT_MS)
    const timer = setTimeout(logout, delay)
    return () => clearTimeout(timer)
  }, [session, logout])

  const value = useMemo<AuthContextValue>(() => ({ session, login, logout }), [session, login, logout])

  return <AuthContext value={value}>{children}</AuthContext>
}
