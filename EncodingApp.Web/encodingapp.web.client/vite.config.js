import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
    plugins: [react()],
    server: {
        port: 60531,
        strictPort: true,
        host: '127.0.0.1',
        proxy: {
            '/api': {
                target: 'http://localhost:5120', 
                changeOrigin: true, 
                secure: false
            }
        }
    }
});