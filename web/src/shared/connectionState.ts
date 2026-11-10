import type { StatusPresentation } from './status'

export type ConnectionState = 'checking' | 'online' | 'offline'

export const connectionPresentation: Record<ConnectionState, StatusPresentation> = {
  checking: { label: 'Łączenie…', icon: 'pi pi-spin pi-spinner', severity: 'warn' },
  online: { label: 'Połączono', icon: 'pi pi-check-circle', severity: 'success' },
  offline: { label: 'Brak połączenia', icon: 'pi pi-times-circle', severity: 'danger' },
}
