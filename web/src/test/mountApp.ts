import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Component } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'

import type { TokenUsage, WorkItemSummary } from '@/api/types'

const Empty = { template: '<div />' }

/** Montuje komponent tak jak w aplikacji: ze świeżym store'em i routerem. */
export async function mountApp(component: Component, props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)

  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'dashboard', component: Empty },
      { path: '/kanban', name: 'kanban', component: Empty },
      { path: '/work-items/:id', name: 'work-item', component: Empty },
    ],
  })

  const wrapper = mount(component, { props, global: { plugins: [pinia, router] } })
  await flushPromises()
  return wrapper
}

export const noUsage: TokenUsage = {
  model: null,
  inputTokens: 0,
  outputTokens: 0,
  cacheReadTokens: 0,
  cacheWriteTokens: 0,
  totalTokens: 0,
  cost: 0,
}

export const workItem = (overrides: Partial<WorkItemSummary> = {}): WorkItemSummary => ({
  id: 'id-1',
  key: 'PROJ-1',
  title: 'Zła stawka VAT',
  type: 'Bug',
  boardColumn: 'todo',
  createdAt: '2026-11-11T08:00:00Z',
  currentStepName: 'Planowanie',
  currentStepStatus: 'Pending',
  waitsForUser: false,
  stepsDone: 1,
  stepsTotal: 4,
  repositories: ['Billing.Api'],
  usage: noUsage,
  ...overrides,
})
