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
        // ── Page background ──
        'app.bg': {
          value: { _dark: '#080c14', _light: '#f0f4f8' },
        },
        // ── Sticky header ──
        'app.headerBg': {
          value: { _dark: 'rgba(8,12,20,0.92)', _light: 'rgba(240,244,248,0.96)' },
        },
        // ── Dividers / borders ──
        'app.border': {
          value: { _dark: 'rgba(255,255,255,0.05)', _light: 'rgba(30,60,100,0.08)' },
        },
        // ── Body text ──
        'app.text': {
          value: { _dark: '#e5e7eb', _light: '#0d1f3c' },
        },
        // ── Muted label text ──
        'app.textMuted': {
          value: { _dark: '#6b7280', _light: '#3d5a80' },
        },
        // ── Dimmer muted (section labels, counters) ──
        'app.textDim': {
          value: { _dark: '#374151', _light: '#7a99bb' },
        },
        // ── Scrollbar thumb ──
        'app.scrollThumb': {
          value: { _dark: 'rgba(255,255,255,0.2)', _light: 'rgba(30,60,100,0.2)' },
        },

        // ── Card / tile surfaces ──
        'app.cardBg': {
          value: { _dark: 'rgba(255,255,255,0.04)', _light: '#ffffff' },
        },
        'app.cardBgHover': {
          value: { _dark: 'rgba(255,255,255,0.08)', _light: '#f4f8ff' },
        },
        'app.cardBorder': {
          value: { _dark: 'rgba(255,255,255,0.08)', _light: 'rgba(30,60,100,0.12)' },
        },
        'app.cardBorderHover': {
          value: { _dark: 'rgba(255,255,255,0.18)', _light: 'rgba(30,60,100,0.28)' },
        },

        // ── Dialog / panel backgrounds ──
        'app.panelBg': {
          value: { _dark: '#111827', _light: '#ffffff' },
        },
        'app.panelBorder': {
          value: { _dark: 'rgba(255,255,255,0.08)', _light: 'rgba(30,60,100,0.12)' },
        },

        // ── Inputs / textareas ──
        'app.inputBg': {
          value: { _dark: 'rgba(0,0,0,0.35)', _light: 'rgba(255,255,255,0.9)' },
        },
        'app.inputBorder': {
          value: { _dark: 'rgba(255,255,255,0.1)', _light: 'rgba(30,60,100,0.15)' },
        },
        'app.inputColor': {
          value: { _dark: '#e5e7eb', _light: '#0d1f3c' },
        },
        'app.placeholder': {
          value: { _dark: '#374151', _light: '#7a99bb' },
        },

        // ── Icon buttons ──
        'app.iconColor': {
          value: { _dark: '#6b7280', _light: '#3d5a80' },
        },

        // ── Terminal / log container ──
        'app.termBg': {
          value: { _dark: 'rgba(0,0,0,0.6)', _light: 'rgba(30,60,100,0.04)' },
        },

        // ── Zebra-striped table rows ──
        'app.rowStripe': {
          value: { _dark: 'rgba(0,0,0,0.2)', _light: 'rgba(30,60,100,0.04)' },
        },
      },
    },
  },
})

export const system = createSystem(defaultConfig, config)
