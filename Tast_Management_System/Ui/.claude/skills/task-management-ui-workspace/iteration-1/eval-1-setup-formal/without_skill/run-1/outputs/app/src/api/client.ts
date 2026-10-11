/** Base URL of TaskManagement.Api. Set VITE_API_BASE_URL in .env.development / .env.production. */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

/**
 * Error body returned by every failing API call (RFC 7807 ProblemDetails + code + traceId),
 * built by Api/Infrastructure/ApiProblems.cs.
 */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  code?: string
  traceId?: string
  /** Validation errors keyed by C#-style field name, e.g. { "Username": ["..."] }. */
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  get code(): string | undefined {
    return this.problem?.code
  }
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

/** POSTs JSON and returns the parsed JSON response, throwing ApiError for non-2xx responses. */
export async function postJson<TResponse>(
  path: string,
  body: unknown,
  init?: { signal?: AbortSignal; accessToken?: string },
): Promise<TResponse> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (init?.accessToken) headers.Authorization = `Bearer ${init.accessToken}`

  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: 'POST',
      headers,
      body: JSON.stringify(body),
      signal: init?.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, null, 'Cannot reach the server. Check that the API is running and try again.')
  }

  if (!response.ok) {
    const problem = await readProblem(response)
    throw new ApiError(
      response.status,
      problem,
      problem?.detail ?? problem?.title ?? `Request failed with status ${response.status}.`,
    )
  }

  return (await response.json()) as TResponse
}
