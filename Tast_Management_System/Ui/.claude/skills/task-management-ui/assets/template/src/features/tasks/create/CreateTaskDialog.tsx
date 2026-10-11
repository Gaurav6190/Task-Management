import { useEffect, useRef } from 'react'
import type { CreateTaskRequest } from '../taskTypes'
import { CreateTaskForm } from './CreateTaskForm'
import styles from './CreateTaskDialog.module.css'

interface CreateTaskDialogProps {
  open: boolean
  onClose: () => void
  /** Creates the task. Rejects with the API error so the form can show it. */
  onCreate: (request: CreateTaskRequest) => Promise<unknown>
}

/**
 * Modal "New task" dialog built on the native <dialog>: it traps focus, closes on Esc, and returns focus
 * to the button that opened it. The form only exists while the dialog is open, so it starts empty each time.
 */
export function CreateTaskDialog({ open, onClose, onCreate }: CreateTaskDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={dialogRef}
      className={styles.dialog}
      aria-labelledby="create-task-title"
      onCancel={(e) => {
        // Esc: let React state decide, so `open` and the element never disagree.
        e.preventDefault()
        onClose()
      }}
    >
      <header className={styles.header}>
        <h2 id="create-task-title" className={styles.title}>
          New task
        </h2>
        <button type="button" className={styles.close} onClick={onClose} aria-label="Close">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M6 6l12 12M18 6L6 18" />
          </svg>
        </button>
      </header>

      {open && (
        <CreateTaskForm
          onSubmit={async (request) => {
            await onCreate(request)
            onClose()
          }}
          onCancel={onClose}
        />
      )}
    </dialog>
  )
}
