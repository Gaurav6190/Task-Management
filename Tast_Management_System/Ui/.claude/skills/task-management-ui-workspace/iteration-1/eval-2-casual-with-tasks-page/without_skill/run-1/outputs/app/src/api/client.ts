const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7213').replace(/\/+$/, '')

/** Error shape the API returns for every failure (RFC 9457 problem details + code/traceId/errors). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem?: ProblemDetails

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export async function postJson<TResponse>(path: string, body: unknown): Promise<TResponse> {
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, `Can't reach the server at ${API_BASE_URL}. Is the API running?`)
  }

  if (!response.ok) {
    const problem = await readProblem(response)
    throw new ApiError(response.status, problemMessage(response.status, problem), problem)
  }

  return (await response.json()) as TResponse
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return undefined
  }
}

function problemMessage(status: number, problem?: ProblemDetails): string {
  const fieldErrors = problem?.errors ? Object.values(problem.errors).flat() : []
  if (fieldErrors.length > 0) return fieldErrors.join(' ')
  if (problem?.detail) return problem.detail
  if (status >= 500) return 'Something went wrong on the server. Please try again.'
  return problem?.title ?? `Request failed (${status}).`
}
