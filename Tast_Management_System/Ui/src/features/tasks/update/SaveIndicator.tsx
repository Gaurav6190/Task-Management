import styles from './SaveIndicator.module.css'

interface SaveIndicatorProps {
  state: 'saving' | 'saved' | null
  /** What is being saved, for screen readers: "Status", "Priority". */
  label: string
}

/**
 * Small spinner while a change is being saved, then a tick once the API has confirmed it.
 * The live region is always rendered, so screen readers announce "Status saved".
 */
export function SaveIndicator({ state, label }: SaveIndicatorProps) {
  return (
    <span className={styles.indicator}>
      {state === 'saving' && <span className={styles.spinner} aria-hidden="true" />}
      {state === 'saved' && (
        <svg className={styles.tick} viewBox="0 0 24 24" aria-hidden="true">
          <path d="M5 12l5 5 9-10" />
        </svg>
      )}
      <span className={styles.srOnly} aria-live="polite">
        {state === 'saving' ? `Saving ${label.toLowerCase()}…` : state === 'saved' ? `${label} saved` : ''}
      </span>
    </span>
  )
}
