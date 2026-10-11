import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { login } from '../api/auth'
import { AuthContext, type AuthContextValue } from './AuthContext'
import { clearSession, loadSession, saveSession, sessionFromLogin, type Session } from './session'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(loadSession)

  const signIn = useCallback(async (username: string, password: string) => {
    const next = sessionFromLogin(await login({ username, password }))
    saveSession(next)
    setSession(next)
  }, [])

  const signOut = useCallback(() => {
    clearSession()
    setSession(null)
  }, [])

  // The token is valid for 60 minutes and there is no refresh token: sign out when it expires.
  useEffect(() => {
    if (!session) return
    const msLeft = Date.parse(session.expiresAtUtc) - Date.now()
    const timer = window.setTimeout(signOut, Math.max(msLeft, 0))
    return () => window.clearTimeout(timer)
  }, [session, signOut])

  const value = useMemo<AuthContextValue>(() => ({ session, signIn, signOut }), [session, signIn, signOut])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
