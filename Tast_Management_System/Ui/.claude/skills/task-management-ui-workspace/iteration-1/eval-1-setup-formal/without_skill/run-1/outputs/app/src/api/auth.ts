import { postJson } from './client'

/** Mirrors Api/Contracts/Auth/AuthContracts.cs -> LoginRequest. */
export interface LoginRequest {
  /** Email address. */
  username: string
  password: string
}

/** Mirrors Api/Contracts/Auth/AuthContracts.cs -> LoginResponse. */
export interface LoginResponse {
  accessToken: string
  /** Always "Bearer". */
  tokenType: string
  /** ISO 8601 UTC. After this the API returns 401 and the user must log in again. */
  expiresAtUtc: string
  username: string
}

/** Limits enforced by LoginCommandValidator in the API. */
export const USERNAME_MAX_LENGTH = 256
export const PASSWORD_MAX_LENGTH = 128

/** POST /api/auth/login (AuthController.Login). */
export function login(request: LoginRequest, signal?: AbortSignal): Promise<LoginResponse> {
  return postJson<LoginResponse>('/api/auth/login', request, { signal })
}
