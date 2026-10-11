// The API stores and returns UTC. Due dates are calendar days sent as midnight UTC
// ("2026-10-20T00:00:00Z"), so they are read and shown in UTC to keep the same day everywhere.

const DATE_INPUT_PATTERN = /^\d{4}-\d{2}-\d{2}$/

/** .NET may omit the "Z" on UTC DateTimes; without it Date.parse would read the value as local time. */
export function parseUtc(value: string): number {
  const hasZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value)
  return Date.parse(hasZone ? value : `${value}Z`)
}

/** `<input type="date">` value ("2026-10-20") -> API due date ("2026-10-20T00:00:00Z"); empty -> null. */
export function dateInputToUtcIso(value: string): string | null {
  if (value === '') return null
  if (!DATE_INPUT_PATTERN.test(value) || Number.isNaN(Date.parse(`${value}T00:00:00Z`))) {
    throw new RangeError(`Not a date input value: "${value}"`)
  }
  return `${value}T00:00:00Z`
}

/** API UTC date -> `<input type="date">` value; null -> "". */
export function utcIsoToDateInput(value: string | null): string {
  if (!value) return ''
  return new Date(parseUtc(value)).toISOString().slice(0, 10)
}

/**
 * Today's date as an `<input type="date">` value, in UTC. The API rejects due dates before the
 * current UTC day, so this is the earliest date the create form should offer (`min`).
 */
export function todayUtcDateInput(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10)
}

/** Formats an API due date as a calendar day in the user's locale, e.g. "20 Oct 2026". */
export function formatUtcDate(value: string, locale?: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeZone: 'UTC' }).format(parseUtc(value))
}

/** Formats an API timestamp (e.g. completedAtUtc) as a date in the user's own time zone. */
export function formatLocalDate(value: string, locale?: string): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(parseUtc(value))
}

/** True when the due day is before today's UTC day. */
export function isPastUtcDate(value: string, now: Date = new Date()): boolean {
  return utcIsoToDateInput(value) < todayUtcDateInput(now)
}
