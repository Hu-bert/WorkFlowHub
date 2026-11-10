import { describe, expect, it } from 'vitest'

import { formatCost, formatDuration, formatTokens } from '../format'

describe('format', () => {
  it('shortens large token counts', () => {
    expect(formatTokens(950)).toBe('950')
    expect(formatTokens(12_345)).toBe('12,3 tys.')
    expect(formatTokens(2_500_000)).toBe('2,5 mln')
  })

  it('shows a dash instead of zero cost', () => {
    expect(formatCost(0)).toBe('—')
    expect(formatCost(1.5)).toContain('1,50')
  })

  it('formats duration in seconds and minutes', () => {
    expect(formatDuration('2026-11-11T08:00:00Z', '2026-11-11T08:00:42Z')).toBe('42 s')
    expect(formatDuration('2026-11-11T08:00:00Z', '2026-11-11T08:02:05Z')).toBe('2 min 5 s')
    expect(formatDuration(null, null)).toBe('—')
  })
})
