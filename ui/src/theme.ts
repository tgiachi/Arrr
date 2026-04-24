import { createSystem, defaultConfig, defineConfig } from '@chakra-ui/react'

const config = defineConfig({
  globalCss: {
    'html, body': {
      fontFamily: 'Syne, sans-serif',
      bg: '#080c14',
      color: 'gray.100',
    },
    '*': { boxSizing: 'border-box' },
    '::-webkit-scrollbar': { width: '6px' },
    '::-webkit-scrollbar-track': { bg: 'transparent' },
    '::-webkit-scrollbar-thumb': { bg: 'whiteAlpha.200', borderRadius: 'full' },
  },
  theme: {
    tokens: {
      fonts: {
        heading: { value: `'Syne', sans-serif` },
        body: { value: `'Syne', sans-serif` },
        mono: { value: `'JetBrains Mono', monospace` },
      },
    },
  },
})

export const system = createSystem(defaultConfig, config)
