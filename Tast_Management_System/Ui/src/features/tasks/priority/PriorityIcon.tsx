import type { TaskPriority } from '../taskTypes'
import styles from './PriorityIcon.module.css'

// Low ↓, Medium =, High ↑, Critical ⚠. Shape differs per level, so colour is never the only signal.
const PATHS: Record<TaskPriority, string[]> = {
  Low: ['M12 5v14', 'M6 13l6 6 6-6'],
  Medium: ['M5 9h14', 'M5 15h14'],
  High: ['M12 19V5', 'M6 11l6-6 6 6'],
  Critical: ['M12 3l10 18H2z', 'M12 10v4', 'M12 17.5v.01'],
}

/** Decorative priority icon in the priority's colour. The text label next to it carries the meaning. */
export function PriorityIcon({ priority }: { priority: TaskPriority }) {
  return (
    <svg className={styles.icon} data-priority={priority} viewBox="0 0 24 24" aria-hidden="true">
      {PATHS[priority].map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  )
}
