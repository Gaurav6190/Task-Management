import { createContext } from 'react'
import type { Session } from './session'

export interface AuthContextValue {
  session: Session | null
  signIn: (username: string, password: string) => Promise<void>
  signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
