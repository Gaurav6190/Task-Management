import { ApiError, NetworkError } from '../../api/httpClient'

const UNREACHABLE = 'Cannot reach the Task Management API. Make sure it is running, then try again.'

/** One readable sentence for anything thrown by taskApi. 401 is handled separately (sign out). */
export function describeTaskError(error: unknown): string {
  if (error instanceof NetworkError) return UNREACHABLE
  if (error instanceof ApiError) {
    // The Vite proxy answers 5xx with no problem body when the API is not running.
    if (error.status >= 500 && !error.problem) return UNREACHABLE
    return error.problem?.detail ?? `The request failed (status ${error.status}). Please try again.`
  }
  return 'Something went wrong. Please try again.'
}
