import { API_BASE_URL } from '../config'

/**
 * Error body returned by TaskManagement.Api for every failure (application/problem+json).
 * `code` is stable ("Auth.InvalidCredentials", "Validation.Failed", ...) - switch on it, not on `detail`.
 * `errors` keys are C#-style field names ("Username", "Password").
 */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}

/** The API answered with a non-2xx status. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}.`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  get code(): string | undefined {
    return this.problem?.code
  }
}

/** The API could not be reached at all (server down, wrong proxy target, offline). */
export class NetworkError extends Error {
  constructor(cause: unknown) {
    super('Unable to reach the server.', { cause })
    this.name = 'NetworkError'
  }
}

export interface ApiRequestInit extends RequestInit {
  /** JWT access token; sent as "Authorization: Bearer {token}". */
  token?: string
}

export async function apiRequest<T>(path: string, init: ApiRequestInit = {}): Promise<T> {
  const { token, headers: initHeaders, ...rest } = init
  const headers = new Headers(initHeaders)
  headers.set('Accept', 'application/json')
  if (rest.body !== undefined && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { ...rest, headers })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause
    throw new NetworkError(cause)
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response))
  }
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('Content-Type') ?? ''
  if (!contentType.includes('json')) return null
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}
