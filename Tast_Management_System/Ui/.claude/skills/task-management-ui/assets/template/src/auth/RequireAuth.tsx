import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from './useAuth'

/** Layout route: renders child routes when signed in, otherwise redirects to /login and remembers where to return. */
export function RequireAuth() {
  const { session } = useAuth()
  const location = useLocation()

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }
  return <Outlet />
}
