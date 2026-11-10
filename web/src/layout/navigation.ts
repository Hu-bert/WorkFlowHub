export interface NavigationItem {
  label: string
  icon: string
  to: string
  /** Faza, w której pozycja się pojawi. Brak = już dostępna. */
  comingInPhase?: number
}

// Nowy moduł = nowa pozycja tutaj + trasa w router/index.ts.
export const navigation: NavigationItem[] = [
  { label: 'Dashboard', icon: 'mdi-view-dashboard-outline', to: '/' },
  { label: 'Kanban', icon: 'mdi-view-column-outline', to: '/kanban', comingInPhase: 2 },
  { label: 'Repozytoria', icon: 'mdi-source-repository', to: '/repos', comingInPhase: 2 },
  { label: 'Flow', icon: 'mdi-sitemap-outline', to: '/flows', comingInPhase: 8 },
  { label: 'Tokeny', icon: 'mdi-chart-line', to: '/tokens', comingInPhase: 5 },
]
