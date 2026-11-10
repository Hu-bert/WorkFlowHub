import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import { describe, expect, it } from 'vitest'

import DashboardView from '../DashboardView.vue'

describe('DashboardView', () => {
  const mountDashboard = () => mount(DashboardView, { global: { plugins: [PrimeVue] } })

  it('shows title and empty state', () => {
    const wrapper = mountDashboard()

    expect(wrapper.find('h1').text()).toBe('Dashboard')
    expect(wrapper.text()).toContain('Brak zadań')
  })

  it('shows four stat cards', () => {
    expect(mountDashboard().findAll('.stat')).toHaveLength(4)
  })
})
