import { apiRequest } from './httpClient'

/** Body of POST /api/auth/login (TaskManagement.Api LoginRequest). `username` is an email address. */
export interface LoginRequest {
  username: string
  password: string
}

/** 200 response of POST /api/auth/login (TaskManagement.Api LoginResponse). */
export interface LoginResponse {
  accessToken: string
  /** Always "Bearer". */
  tokenType: string
  /** ISO 8601 UTC timestamp. After this the API answers 401 and the user must log in again. */
  expiresAtUtc: string
  username: string
}

export function login(request: LoginRequest, signal?: AbortSignal): Promise<LoginResponse> {
  return apiRequest<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify(request),
    signal,
  })
}
