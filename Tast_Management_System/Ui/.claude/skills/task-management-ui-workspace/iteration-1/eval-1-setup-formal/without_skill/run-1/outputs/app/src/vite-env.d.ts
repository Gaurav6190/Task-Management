/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL of TaskManagement.Api, e.g. https://localhost:7213 */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
