import { definePreset } from '@primeuix/themes'
import Aura from '@primeuix/themes/aura'

// Ciemny motyw w odcieniach fioletu: kolor główny = violet, powierzchnie lekko fioletowe.
const FlowHubPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{violet.50}',
      100: '{violet.100}',
      200: '{violet.200}',
      300: '{violet.300}',
      400: '{violet.400}',
      500: '{violet.500}',
      600: '{violet.600}',
      700: '{violet.700}',
      800: '{violet.800}',
      900: '{violet.900}',
      950: '{violet.950}',
    },
    colorScheme: {
      dark: {
        surface: {
          0: '#ffffff',
          50: '#f4f2fa',
          100: '#e4e0f0',
          200: '#c9c2e0',
          300: '#a79dcb',
          400: '#8478b0',
          500: '#645a8f',
          600: '#4a4270',
          700: '#352f55',
          800: '#241f3d',
          900: '#18142b',
          950: '#0f0c1d',
        },
      },
    },
  },
})

export const flowHubTheme = {
  preset: FlowHubPreset,
  options: { darkModeSelector: '.app-dark' },
}
