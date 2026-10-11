import { postJson } from './client'

export interface LoginRequest {
  username: string
  password: string
}

/** Mirrors TaskManagement.Api's LoginResponse. */
export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresAtUtc: string
  username: string
}

export function login(request: LoginRequest): Promise<LoginResponse> {
  return postJson<LoginResponse>('/api/auth/login', request)
}
