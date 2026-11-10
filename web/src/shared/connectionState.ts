import type { StatusPresentation } from './status'

export type ConnectionState = 'checking' | 'online' | 'offline'

export const connectionPresentation: Record<ConnectionState, StatusPresentation> = {
  checking: { label: 'Łączenie…', icon: 'mdi-loading mdi-spin', color: 'warning' },
  online: { label: 'Połączono', icon: 'mdi-check-circle', color: 'success' },
  offline: { label: 'Brak połączenia', icon: 'mdi-close-circle', color: 'error' },
}
