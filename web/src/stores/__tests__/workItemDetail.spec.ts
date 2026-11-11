import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { fetchPermissions } from '@/api/agent'
import { onPermissionRequested, onPermissionResolved, onStepOutput, onWorkItemChanged } from '@/api/live'
import { approveStep } from '@/api/steps'
import type { StepRun, StepStatus, WorkItemDetails } from '@/api/types'
import { fetchWorkItem } from '@/api/workItems'
import { noUsage, workItem } from '@/test/mountApp'

import { useWorkItemDetail } from '../workItemDetail'

vi.mock('@/api/live')
vi.mock('@/api/steps')
vi.mock('@/api/agent')
vi.mock('@/api/workItems')

const details = (status: StepStatus, log = ''): WorkItemDetails => ({
  summary: workItem({ id: 'item-1' }),
  description: '',
  source: 'Manual',
  sourceUrl: null,
  workDirectory: 'work',
  repos: [],
  steps: [{ id: 'step-1', status, log, usage: noUsage } as StepRun],
})

/** Wywołuje funkcję, którą store zarejestrował na zdarzenie SignalR. */
function emit<H extends (...args: never[]) => void>(subscribe: (handler: H) => unknown, ...args: Parameters<H>) {
  const handler = vi.mocked(subscribe).mock.calls[0]![0]
  handler(...args)
}

describe('workItemDetail store', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setActivePinia(createPinia())
    vi.mocked(fetchWorkItem).mockResolvedValue(details('Proposed'))
    vi.mocked(fetchPermissions).mockResolvedValue([])
  })

  it('keeps the newer live state when a slower HTTP response arrives later', async () => {
    const store = useWorkItemDetail()
    await store.load('item-1')
    vi.mocked(approveStep).mockImplementation(async () => {
      emit(onWorkItemChanged, details('Running')) // SignalR zdążył przed odpowiedzią HTTP
      return details('Approved')
    })

    await store.approve(store.item!.steps[0]!)

    expect(store.item!.steps[0]!.status).toBe('Running')
  })

  it('shows live output while the step runs and the saved log afterwards', async () => {
    const store = useWorkItemDetail()
    await store.load('item-1')

    emit(onStepOutput, 'item-1', 'step-1', 'linia 1\n')
    emit(onStepOutput, 'inne-zadanie', 'step-1', 'obce\n')
    expect(store.outputOf(store.item!.steps[0]!)).toBe('linia 1\n')

    emit(onWorkItemChanged, details('Succeeded', 'linia 1\nlinia 2\n'))
    expect(store.outputOf(store.item!.steps[0]!)).toBe('linia 1\nlinia 2\n')
  })

  it('tracks permission requests of the open work item', async () => {
    const store = useWorkItemDetail()
    await store.load('item-1')
    const request = { id: 'p-1', workItemId: 'item-1', stepRunId: 'step-1', tool: 'write', detail: 'Zapis' }

    emit(onPermissionRequested, request)
    expect(store.permissionFor(store.item!.steps[0]!)).toEqual(request)

    emit(onPermissionResolved, request)
    expect(store.permissionFor(store.item!.steps[0]!)).toBeNull()
  })
})
