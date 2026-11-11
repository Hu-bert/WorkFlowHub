import type { HealthLevel } from '@/api/integrations'

import type { StatusPresentation } from './status'

/** Poziom statusu → kolor i ikona. Tekst zawsze przychodzi z komórki, więc nigdy nie polegamy na samym kolorze. */
export const healthPresentation: Record<HealthLevel, StatusPresentation> = {
  Ok: { icon: 'mdi-check-circle', color: 'success', label: 'OK' },
  Pending: { icon: 'mdi-timer-sand', color: 'warning', label: 'W toku' },
  Warning: { icon: 'mdi-alert', color: 'warning', label: 'Uwaga' },
  Failed: { icon: 'mdi-close-circle', color: 'error', label: 'Problem' },
  Unknown: { icon: 'mdi-minus-circle-outline', color: 'grey', label: 'Brak danych' },
}
