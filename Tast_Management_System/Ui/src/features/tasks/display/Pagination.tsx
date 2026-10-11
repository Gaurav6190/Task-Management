import styles from './Pagination.module.css'

interface PaginationProps {
  page: number
  pageSize: number
  totalPages: number
  totalCount: number
  onPageChange: (page: number) => void
  disabled?: boolean
}

/** "Showing 21–40 of 57", plus Previous / Next and "Page X of Y" when there is more than one page. */
export function Pagination({ page, pageSize, totalPages, totalCount, onPageChange, disabled = false }: PaginationProps) {
  if (totalCount === 0) return null

  const first = (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, totalCount)

  return (
    <nav className={styles.pagination} aria-label="Task pages">
      <span className={styles.range} aria-live="polite">
        Showing {first}–{last} of {totalCount}
      </span>

      {totalPages > 1 && (
        <div className={styles.controls}>
          <button
            type="button"
            className={styles.button}
            onClick={() => onPageChange(page - 1)}
            disabled={disabled || page <= 1}
          >
            Previous
          </button>
          <span className={styles.label}>
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            className={styles.button}
            onClick={() => onPageChange(page + 1)}
            disabled={disabled || page >= totalPages}
          >
            Next
          </button>
        </div>
      )}
    </nav>
  )
}
