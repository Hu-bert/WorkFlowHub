import { beforeEach, describe, expect, it, vi } from 'vitest'

import { fetchWorkItems } from '@/api/workItems'
import { mountApp, workItem } from '@/test/mountApp'

import DashboardView from '../DashboardView.vue'

vi.mock('@/api/workItems')
vi.mock('@/api/catalog', () => ({
  fetchFlows: async () => ({
    flows: [{ type: 'Bug', name: 'Błąd', version: 1, steps: [] }],
    problems: [{ fileName: 'broken.yaml', errors: ["Pole 'name' jest wymagane."] }],
  }),
  fetchRepositories: async () => [],
  fetchBoard: async () => [],
}))

describe('DashboardView', () => {
  beforeEach(() => vi.mocked(fetchWorkItems).mockResolvedValue([]))

  it('shows empty state when there are no work items', async () => {
    const wrapper = await mountApp(DashboardView)

    expect(wrapper.find('h1').text()).toBe('Dashboard')
    expect(wrapper.text()).toContain('Brak zadań')
    expect(wrapper.findAll('.stat')).toHaveLength(4)
  })

  it('highlights items that wait for the user', async () => {
    vi.mocked(fetchWorkItems).mockResolvedValue([
      workItem({ id: 'a', key: 'PROJ-1', waitsForUser: true, currentStepStatus: 'Proposed' }),
      workItem({ id: 'b', key: 'PROJ-2' }),
    ])

    const wrapper = await mountApp(DashboardView)

    expect(wrapper.text()).toContain('Czeka na Ciebie')
    expect(wrapper.findAll('.work-item-card.waits')).toHaveLength(1)
    expect(wrapper.text()).toContain('Do akceptacji')
    expect(wrapper.text()).toContain('Błąd') // nazwa typu z flow zamiast klucza "Bug"
  })

  it('shows problems found in flow files', async () => {
    const wrapper = await mountApp(DashboardView)

    expect(wrapper.text()).toContain('flows/broken.yaml')
    expect(wrapper.text()).toContain("Pole 'name' jest wymagane.")
  })
})
