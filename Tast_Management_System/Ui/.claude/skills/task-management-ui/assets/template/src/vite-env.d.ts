/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Absolute URL of TaskManagement.Api. Empty means same origin (the Vite dev proxy). */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
