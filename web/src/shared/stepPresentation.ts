import type { StepKind, StepStatus } from '@/api/types'

import type { StatusPresentation } from './status'

export const stepStatusPresentation: Record<StepStatus, StatusPresentation> = {
  Pending: { label: 'Oczekuje', icon: 'mdi-circle-outline', color: 'grey' },
  Proposed: { label: 'Do akceptacji', icon: 'mdi-account-clock', color: 'warning' },
  Approved: { label: 'Zatwierdzony', icon: 'mdi-thumb-up-outline', color: 'info' },
  Running: { label: 'W trakcie', icon: 'mdi-loading mdi-spin', color: 'primary' },
  Succeeded: { label: 'Gotowe', icon: 'mdi-check-circle', color: 'success' },
  Failed: { label: 'Błąd', icon: 'mdi-alert-circle', color: 'error' },
  Skipped: { label: 'Pominięty', icon: 'mdi-debug-step-over', color: 'grey' },
}

export const stepKindPresentation: Record<StepKind, { label: string; icon: string }> = {
  Script: { label: 'Skrypt', icon: 'mdi-powershell' },
  Agent: { label: 'Agent', icon: 'mdi-robot-outline' },
  Manual: { label: 'Ręczny', icon: 'mdi-hand-back-right-outline' },
  Check: { label: 'Sprawdzenie', icon: 'mdi-clipboard-check-outline' },
}
