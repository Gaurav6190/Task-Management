/** Base URL of TaskManagement.Api, without a trailing slash. Empty = same origin (Vite dev proxy). */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')
