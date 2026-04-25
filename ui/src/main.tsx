import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ChakraProvider } from '@chakra-ui/react'
import { ThemeProvider } from 'next-themes'
import { system } from './theme'
import App from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ChakraProvider value={system}>
      <ThemeProvider attribute="data-theme" defaultTheme="dark" disableTransitionOnChange={false}>
        <App />
      </ThemeProvider>
    </ChakraProvider>
  </StrictMode>,
)
