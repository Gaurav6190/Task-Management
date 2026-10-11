import { useEffect, useState } from 'react'
import type { TaskDto, TaskPriority, TaskStatus } from '../taskTypes'
import type { TaskFieldChanges } from '../useTasks'

export type EditableField = 'status' | 'priority'

/** useTasks.updateTaskFields: resolves to null when saved, or to an error message. */
type UpdateTaskFields = (task: TaskDto, changes: TaskFieldChanges) => Promise<string | null>

const SAVED_FLASH_MS = 2000

/**
 * Save state for one table row: which field is being saved, which was just saved, and the last error.
 * Only one save per row at a time, because each save PUTs the whole task (D8) and two overlapping
 * PUTs for the same row could undo each other.
 */
export function useRowUpdate(task: TaskDto, updateTaskFields: UpdateTaskFields) {
  const [saving, setSaving] = useState<EditableField | null>(null)
  const [saved, setSaved] = useState<EditableField | null>(null)
  const [error, setError] = useState<{ field: EditableField; message: string } | null>(null)

  // "Saved" shows briefly, then fades back to nothing.
  useEffect(() => {
    if (!saved) return
    const timer = setTimeout(() => setSaved(null), SAVED_FLASH_MS)
    return () => clearTimeout(timer)
  }, [saved])

  async function save(field: EditableField, changes: TaskFieldChanges) {
    if (saving) return
    setSaving(field)
    setSaved(null)
    setError(null)

    const message = await updateTaskFields(task, changes)

    setSaving(null)
    if (message) setError({ field, message })
    else setSaved(field)
  }

  return {
    saving,
    saved,
    error,
    changeStatus(status: TaskStatus) {
      if (status !== task.status) void save('status', { status })
    },
    changePriority(priority: TaskPriority) {
      if (priority !== task.priority) void save('priority', { priority })
    },
  }
}
