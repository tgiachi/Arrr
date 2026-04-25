import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../src/Arrr.Service/wwwroot',
    emptyOutDir: true,
    chunkSizeWarningLimit: 700,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom'],
          chakra: ['@chakra-ui/react'],
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5150',
        changeOrigin: true,
      },
      '/stream': {
        target: 'http://localhost:5150',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
