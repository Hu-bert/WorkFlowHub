import '@mdi/font/css/materialdesignicons.css'
import 'vuetify/styles'

import { createVuetify, type ThemeDefinition } from 'vuetify'
import { pl } from 'vuetify/locale'

// Ciemny motyw w odcieniach fioletu. Kolory statusów (success/warning/error/info) używane w całym UI.
const flowHubDark: ThemeDefinition = {
  dark: true,
  colors: {
    background: '#0f0c1d',
    surface: '#1b1631',
    'surface-bright': '#2a2347',
    'surface-light': '#241f3d',
    'surface-variant': '#352f55',
    'on-surface-variant': '#ede9fe',
    primary: '#8b5cf6',
    'primary-darken-1': '#7c3aed',
    secondary: '#c026d3',
    'secondary-darken-1': '#a21caf',
    success: '#22c55e',
    warning: '#f59e0b',
    error: '#f43f5e',
    info: '#38bdf8',
  },
}

export const vuetify = createVuetify({
  theme: {
    defaultTheme: 'flowHubDark',
    themes: { flowHubDark },
  },
  locale: { locale: 'pl', messages: { pl } },
  defaults: {
    VCard: { rounded: 'xl', border: true, flat: true },
    VBtn: { rounded: 'lg', class: 'text-none' },
    VChip: { rounded: 'pill' },
    VTextField: { variant: 'outlined', density: 'comfortable' },
    VTextarea: { variant: 'outlined', density: 'comfortable' },
    VSelect: { variant: 'outlined', density: 'comfortable' },
    VAutocomplete: { variant: 'outlined', density: 'comfortable' },
  },
})
