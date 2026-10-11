import { ApiError, NetworkError } from '../api/httpClient'

export interface LoginFailure {
  /** Message shown above the form. */
  form: string | null
  username: string | null
  password: string | null
}

const UNREACHABLE = 'Cannot reach the Task Management API. Make sure it is running, then try again.'

/** Turns anything thrown by authApi.login into messages for the login form. */
export function describeLoginError(error: unknown): LoginFailure {
  const failure: LoginFailure = { form: null, username: null, password: null }

  if (error instanceof NetworkError) {
    return { ...failure, form: UNREACHABLE }
  }
  if (!(error instanceof ApiError)) {
    return { ...failure, form: 'Something went wrong. Please try again.' }
  }

  // The Vite proxy answers 5xx with no problem body when the API is not running.
  if (error.status >= 500 && !error.problem) {
    return { ...failure, form: UNREACHABLE }
  }

  if (error.code === 'Auth.InvalidCredentials' || error.status === 401) {
    // Same message for unknown email and wrong password: never reveal which accounts exist.
    return { ...failure, form: 'Invalid username or password.' }
  }

  if (error.status === 400 && error.problem?.errors) {
    const errors = error.problem.errors
    const fieldError = (name: string) =>
      Object.entries(errors).find(([key]) => key.toLowerCase() === name)?.[1]?.[0] ?? null
    const username = fieldError('username')
    const password = fieldError('password')
    return {
      form: username || password ? null : (error.problem.detail ?? 'Please check your details.'),
      username,
      password,
    }
  }

  return { ...failure, form: error.problem?.detail ?? 'Sign in failed. Please try again.' }
}
