import styles from './ColumnFilter.module.css'

export interface FilterOption<T extends string> {
  value: T
  label: string
}

interface ColumnFilterProps<T extends string> {
  /** Accessible name, e.g. "Filter by status". */
  label: string
  /** Selected value, or undefined for "All". */
  value: T | undefined
  options: readonly FilterOption<T>[]
  onChange: (value: T | undefined) => void
}

/** Compact filter dropdown that sits inside a column header, under the column name. */
export function ColumnFilter<T extends string>({ label, value, options, onChange }: ColumnFilterProps<T>) {
  return (
    <select
      className={value ? `${styles.select} ${styles.active}` : styles.select}
      aria-label={label}
      value={value ?? ''}
      onChange={(e) => onChange(options.find((option) => option.value === e.target.value)?.value)}
    >
      <option value="">All</option>
      {options.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  )
}
