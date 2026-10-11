import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../api/httpClient'
import { useAuth } from '../../auth/useAuth'
import * as taskApi from './taskApi'
import { describeTaskError } from './taskErrors'
import {
  DEFAULT_PAGE_SIZE,
  type CreateTaskRequest,
  type PagedResult,
  type TaskDto,
  type TaskPriority,
  type TaskStatus,
} from './taskTypes'

export interface TaskFilters {
  status?: TaskStatus
  priority?: TaskPriority
}

/** Fields that can be edited inline on a row (T4). */
export type TaskFieldChanges = { status: TaskStatus } | { priority: TaskPriority }

/** Which request a result belongs to, so a slow response never overwrites a newer one. */
interface Settled {
  key: string
  error: string | null
}

/** Loads one page of tasks for the signed-in user, and creates / updates tasks through the API. */
export function useTasks(pageSize: number = DEFAULT_PAGE_SIZE) {
  const { session, logout } = useAuth()
  const token = session?.accessToken

  const [page, setPage] = useState(1)
  const [filters, setFiltersState] = useState<TaskFilters>({})
  const [reloadCount, setReloadCount] = useState(0)
  // The last page that loaded. Kept while the next one loads, so the list dims instead of flashing empty.
  const [data, setData] = useState<PagedResult<TaskDto> | null>(null)
  const [settled, setSettled] = useState<Settled | null>(null)
  // Page-level message, e.g. when a row disappears because the task was deleted elsewhere.
  const [notice, setNotice] = useState<string | null>(null)

  const key = `${page}|${pageSize}|${filters.status ?? ''}|${filters.priority ?? ''}|${reloadCount}`

  useEffect(() => {
    if (!token) return
    const controller = new AbortController()

    taskApi.listTasks({ page, pageSize, ...filters }, token, controller.signal).then(
      (next) => {
        if (controller.signal.aborted) return
        setData(next)
        setSettled({ key, error: null })
      },
      (error: unknown) => {
        if (controller.signal.aborted) return
        if (taskApi.isUnauthorized(error)) {
          logout() // token expired or revoked: back to /login
          return
        }
        setSettled({ key, error: describeTaskError(error) })
      },
    )

    return () => controller.abort()
  }, [key, token, page, pageSize, filters, logout])

  /** Changing a filter starts again from page 1. */
  const setFilters = useCallback((next: TaskFilters) => {
    setFiltersState(next)
    setPage(1)
  }, [])

  const reload = useCallback(() => setReloadCount((count) => count + 1), [])
  const dismissNotice = useCallback(() => setNotice(null), [])

  const replaceTask = useCallback((task: TaskDto) => {
    setData((current) =>
      current && { ...current, items: current.items.map((item) => (item.id === task.id ? task : item)) },
    )
  }, [])

  const removeTask = useCallback((id: string) => {
    setData((current) =>
      current && {
        ...current,
        items: current.items.filter((item) => item.id !== id),
        totalCount: Math.max(current.totalCount - 1, 0),
      },
    )
  }, [])

  /**
   * T3: creates a task, then shows page 1 (newest first) so the new task is at the top.
   * Filters are cleared too, in case they would hide it. Throws on failure so the form can show the errors.
   */
  const createTask = useCallback(
    async (request: CreateTaskRequest): Promise<string> => {
      if (!token) throw new Error('Not signed in.')
      try {
        const id = await taskApi.createTask(request, token)
        setFiltersState({})
        setPage(1)
        reload()
        return id
      } catch (error) {
        if (taskApi.isUnauthorized(error)) logout()
        throw error
      }
    },
    [token, logout, reload],
  )

  /**
   * T4: saves a status or priority change for one row to the database (D3, D11).
   * 1. shows the change straight away, 2. PUTs the whole task (D8), 3. on 204 replaces the row with the
   * database's copy. On failure the row goes back to its saved values.
   * Resolves to null when saved, or to an error message for the row.
   */
  const updateTaskFields = useCallback(
    async (task: TaskDto, changes: TaskFieldChanges): Promise<string | null> => {
      if (!token) return 'Not signed in.'
      replaceTask({ ...task, ...changes })

      try {
        await taskApi.updateTask(task.id, taskApi.toUpdateRequest(task, changes), token)
      } catch (error) {
        if (taskApi.isUnauthorized(error)) {
          logout()
          return 'Your session has expired.'
        }
        if (error instanceof ApiError && error.status === 404) {
          removeTask(task.id)
          setNotice(`"${task.title}" no longer exists. It may have been deleted by someone else.`)
          return null
        }
        replaceTask(task) // revert to the last saved values
        if (error instanceof ApiError && error.status === 409) {
          setNotice('A task was changed by someone else at the same time. The list has been refreshed; please try again.')
          reload()
          return 'Not saved: changed by someone else.'
        }
        return `Not saved. ${describeTaskError(error)}`
      }

      // Saved. Show exactly what the database now holds (ModifiedDate, CompletedAtUtc, ...).
      try {
        replaceTask(await taskApi.getTask(task.id, token))
      } catch {
        // The save itself succeeded; keep the optimistic row until the next reload.
      }
      return null
    },
    [token, logout, replaceTask, removeTask, reload],
  )

  return {
    tasks: data?.items ?? [],
    totalCount: data?.totalCount ?? 0,
    totalPages: data?.totalPages ?? 0,
    page,
    pageSize,
    setPage,
    filters,
    setFilters,
    /** True from the first request until a response for the current page/filters arrives. */
    loading: settled?.key !== key,
    /** Error for the current page/filters, if the last request failed. */
    error: settled?.key === key ? settled.error : null,
    /** False until the first page has loaded at least once. */
    hasLoaded: data !== null,
    reload,
    createTask,
    updateTaskFields,
    notice,
    dismissNotice,
  }
}
