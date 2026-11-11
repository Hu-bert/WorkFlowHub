import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import BudgetBar from '../BudgetBar.vue'

const text = (used: number, limit: number) => mount(BudgetBar, { props: { used, limit, label: 'Budżet kroku' } }).text()

describe('BudgetBar', () => {
  it('shows usage and percent', () => {
    expect(text(500, 1000)).toContain('Budżet kroku: 500 / 1000 (50%)')
  })

  it('warns in words from 80 percent and reports exceeded from 100', () => {
    expect(text(500, 1000)).not.toContain('uwaga')
    expect(text(800, 1000)).toContain('uwaga, ponad 80%')
    expect(text(1200, 1000)).toContain('budżet przekroczony')
  })

  it('is hidden when there is no limit', () => {
    expect(text(500, 0)).toBe('')
  })
})
