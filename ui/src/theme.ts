import { createSystem, defaultConfig, defineConfig } from '@chakra-ui/react'

const config = defineConfig({
  globalCss: {
    'html, body': {
      fontFamily: 'Syne, sans-serif',
      bg: 'app.bg',
      color: 'app.text',
    },
    '*': { boxSizing: 'border-box' },
    '::-webkit-scrollbar': { width: '6px' },
    '::-webkit-scrollbar-track': { bg: 'transparent' },
    '::-webkit-scrollbar-thumb': { bg: 'app.scrollThumb', borderRadius: 'full' },
  },
  theme: {
    tokens: {
      fonts: {
        heading: { value: `'Syne', sans-serif` },
        body: { value: `'Syne', sans-serif` },
        mono: { value: `'JetBrains Mono', monospace` },
      },
    },
    semanticTokens: {
      colors: {
        // Main background — deep navy dark / aged parchment light
        'app.bg': {
          value: { _dark: '#080c14', _light: '#f0e6d0' },
        },
        // Sticky header — semi-transparent tinted panel
        'app.headerBg': {
          value: { _dark: 'rgba(8,12,20,0.92)', _light: 'rgba(240,230,208,0.94)' },
        },
        // Subtle divider / border lines
        'app.border': {
          value: { _dark: 'rgba(255,255,255,0.05)', _light: 'rgba(0,0,0,0.08)' },
        },
        // Body text (outside cards)
        'app.text': {
          value: { _dark: '#e5e7eb', _light: '#3d2b1a' },
        },
        // Muted label text
        'app.textMuted': {
          value: { _dark: '#6b7280', _light: '#7a5c3a' },
        },
        // Dimmer muted (section labels, counters)
        'app.textDim': {
          value: { _dark: '#374151', _light: '#b0916e' },
        },
        // Scrollbar thumb
        'app.scrollThumb': {
          value: { _dark: 'rgba(255,255,255,0.2)', _light: 'rgba(0,0,0,0.15)' },
        },
      },
    },
  },
})

export const system = createSystem(defaultConfig, config)
