export interface NavigationItem {
  label: string
  icon: string
  to: string
  /** Faza, w której pozycja się pojawi. Brak = już dostępna. */
  comingInPhase?: number
}

// Nowy moduł = nowa pozycja tutaj + trasa w router/index.ts.
export const navigation: NavigationItem[] = [
  { label: 'Dashboard', icon: 'pi pi-th-large', to: '/' },
  { label: 'Kanban', icon: 'pi pi-objects-column', to: '/kanban', comingInPhase: 2 },
  { label: 'Repozytoria', icon: 'pi pi-database', to: '/repos', comingInPhase: 2 },
  { label: 'Flow', icon: 'pi pi-sitemap', to: '/flows', comingInPhase: 8 },
  { label: 'Tokeny', icon: 'pi pi-chart-line', to: '/tokens', comingInPhase: 5 },
]
