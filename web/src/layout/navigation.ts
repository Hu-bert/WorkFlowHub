export interface NavigationItem {
  label: string
  icon: string
  to: string
}

// Nowy moduł = nowa pozycja tutaj + trasa w router/index.ts.
export const navigation: NavigationItem[] = [
  { label: 'Dashboard', icon: 'mdi-view-dashboard-outline', to: '/' },
  { label: 'Kanban', icon: 'mdi-view-column-outline', to: '/kanban' },
  { label: 'Repozytoria', icon: 'mdi-source-repository', to: '/repos' },
  { label: 'Flow i bloczki', icon: 'mdi-sitemap-outline', to: '/flows' },
  { label: 'Tokeny', icon: 'mdi-chart-line', to: '/tokens' },
  { label: 'Ustawienia', icon: 'mdi-cog-outline', to: '/settings' },
]
