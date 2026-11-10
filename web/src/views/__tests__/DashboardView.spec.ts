import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import DashboardView from '../DashboardView.vue'

describe('DashboardView', () => {
  it('shows title and empty state', () => {
    const wrapper = mount(DashboardView)

    expect(wrapper.find('h1').text()).toBe('Dashboard')
    expect(wrapper.text()).toContain('Brak zadań')
  })

  it('shows four stat cards', () => {
    expect(mount(DashboardView).findAll('.stat')).toHaveLength(4)
  })
})
