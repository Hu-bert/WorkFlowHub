import { beforeEach, describe, expect, it, vi } from 'vitest'

import { approveStep, editProposal, retryStep } from '@/api/steps'
import type { StepRun } from '@/api/types'
import { mountApp, noUsage } from '@/test/mountApp'

import StepPanel from '../StepPanel.vue'

vi.mock('@/api/steps')
vi.mock('@/api/live', () => ({
  onWorkItemChanged: vi.fn(),
  onStepOutput: vi.fn(),
  onPermissionRequested: vi.fn(),
  onPermissionResolved: vi.fn(),
}))

const step = (overrides: Partial<StepRun> = {}): StepRun => ({
  id: 'step-1',
  stepId: 'plan',
  name: 'Planowanie',
  kind: 'Agent',
  order: 3,
  repoName: null,
  status: 'Proposed',
  requiresApproval: true,
  proposal: 'Przygotuj plan',
  model: 'tier:cheap',
  tokenBudget: 40000,
  result: null,
  error: null,
  remark: null,
  log: '',
  attempt: 1,
  startedAt: null,
  finishedAt: null,
  usage: noUsage,
  ...overrides,
})

describe('StepPanel', () => {
  beforeEach(() => vi.clearAllMocks())

  it('approves the proposal as it is', async () => {
    const wrapper = await mountApp(StepPanel, { step: step() })

    await wrapper.findAll('button').find((button) => button.text().includes('Zatwierdź i uruchom'))!.trigger('click')

    expect(editProposal).not.toHaveBeenCalled()
    expect(approveStep).toHaveBeenCalledWith('step-1')
  })

  it('saves the edited prompt before approving, so what runs is what the user sees', async () => {
    const wrapper = await mountApp(StepPanel, { step: step() })

    await wrapper.find('textarea').setValue('Przygotuj plan. Najpierw biblioteka.')
    await wrapper.findAll('button').find((button) => button.text().includes('Zatwierdź i uruchom'))!.trigger('click')
    await vi.waitFor(() => expect(approveStep).toHaveBeenCalled())

    expect(editProposal).toHaveBeenCalledWith('step-1', {
      proposal: 'Przygotuj plan. Najpierw biblioteka.',
      model: 'tier:cheap',
      tokenBudget: 40000,
    })
  })

  it('shows the error and lets the user retry with a remark', async () => {
    const wrapper = await mountApp(StepPanel, { step: step({ status: 'Failed', error: 'Błąd kompilacji' }) })

    expect(wrapper.text()).toContain('Błąd kompilacji')

    await wrapper.find('input').setValue('Użyj .NET 10')
    await wrapper.findAll('button').find((button) => button.text().includes('Powtórz'))!.trigger('click')

    expect(retryStep).toHaveBeenCalledWith('step-1', 'Użyj .NET 10')
  })

  it('manual step is confirmed, not "run"', async () => {
    const wrapper = await mountApp(StepPanel, { step: step({ kind: 'Manual', proposal: '☐ Sprawdź plan' }) })

    expect(wrapper.text()).toContain('Potwierdzam — zrobione')
    expect(wrapper.text()).not.toContain('Budżet tokenów')
  })
})
