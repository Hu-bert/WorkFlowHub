import { createRouter, createWebHistory } from 'vue-router'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: () => import('@/views/DashboardView.vue') },
    { path: '/kanban', name: 'kanban', component: () => import('@/views/KanbanView.vue') },
    { path: '/repos', name: 'repos', component: () => import('@/views/ReposView.vue') },
    {
      path: '/work-items/:id',
      name: 'work-item',
      component: () => import('@/views/WorkItemView.vue'),
      props: true,
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})
