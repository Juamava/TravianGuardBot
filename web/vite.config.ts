import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  // npm run dev talks to the running bot.
  server: { proxy: { '/api': 'http://127.0.0.1:17891' } },
  build: {
    outDir: '../WPFUI/wwwroot',
    emptyOutDir: true,
  },
})
