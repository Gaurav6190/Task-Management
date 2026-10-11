// Client for TaskManagement.Api AuthController: POST /api/auth/login

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

/** Mirrors Contracts/Auth/LoginRequest. Username is an email address. */
export interface LoginRequest {
  username: string
  password: string
}

/** Mirrors Contracts/Auth/LoginResponse. */
export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresAtUtc: string
  username: string
}

/** ASP.NET Core ProblemDetails / ValidationProblemDetails. */
interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

export async function login(request: LoginRequest, signal?: AbortSignal): Promise<LoginResponse> {
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(request),
      signal,
    })
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') throw err
    throw new ApiError('Cannot reach the server. Check that the API is running and try again.', 0)
  }

  if (response.ok) {
    return (await response.json()) as LoginResponse
  }

  const problem = await readProblem(response)
  if (response.status === 401) {
    throw new ApiError(problem?.detail ?? 'Invalid username or password.', 401)
  }
  if (response.status === 400) {
    throw new ApiError(
      problem?.detail ?? 'Please correct the highlighted fields.',
      400,
      normaliseKeys(problem?.errors ?? {}),
    )
  }
  throw new ApiError(
    problem?.detail ?? `Sign-in failed (${response.status}). Please try again later.`,
    response.status,
  )
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

/** The API may key errors as "Username" or "username"; normalise to lower camel case. */
function normaliseKeys(errors: Record<string, string[]>): Record<string, string[]> {
  const result: Record<string, string[]> = {}
  for (const [key, messages] of Object.entries(errors)) {
    const k = key.charAt(0).toLowerCase() + key.slice(1)
    result[k] = [...(result[k] ?? []), ...messages]
  }
  return result
}
