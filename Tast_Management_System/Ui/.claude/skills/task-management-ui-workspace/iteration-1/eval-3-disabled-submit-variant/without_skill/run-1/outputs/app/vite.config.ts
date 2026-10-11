import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  // TaskManagement.Api "https" launch profile. The dev server proxies /api to it,
  // so the browser talks to one origin and the API's CORS list doesn't matter in dev.
  const apiTarget = env.VITE_API_PROXY_TARGET || 'https://localhost:7213'

  return {
    plugins: [react()],
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
          // Accept the ASP.NET Core dev certificate.
          secure: false,
        },
      },
    },
  }
})
