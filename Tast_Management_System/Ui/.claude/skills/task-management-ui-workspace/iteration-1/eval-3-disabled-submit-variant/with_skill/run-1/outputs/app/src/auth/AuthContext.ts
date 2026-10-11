import { createContext } from 'react'
import type { LoginRequest } from '../api/authApi'
import type { Session } from './session'

export interface AuthContextValue {
  session: Session | null
  login: (credentials: LoginRequest) => Promise<void>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
