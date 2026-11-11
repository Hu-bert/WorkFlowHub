import { flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { emptyStep, fetchFlowDocument, saveFlow, type FlowDocument } from '@/api/flowEditor'
import { mountApp } from '@/test/mountApp'

import FlowEditor from '../FlowEditor.vue'

vi.mock('@/api/flowEditor', async (original) => ({
  ...(await original<typeof import('@/api/flowEditor')>()),
  fetchFlowDocument: vi.fn(),
  saveFlow: vi.fn(),
}))

const flow = { type: 'Bug', name: 'Błąd', version: 1, steps: [] }

vi.mock('@/api/catalog', () => ({
  fetchFlows: async () => ({ flows: [flow], problems: [] }),
  fetchRepositories: async () => [],
  fetchBoard: async () => [],
}))
vi.mock('@/api/tokens', () => ({ fetchModels: async () => null }))

const document = (): FlowDocument => ({
  type: 'Bug',
  name: 'Błąd',
  version: 1,
  flow: [
    { ...emptyStep('Script'), id: 'one', name: 'Pierwszy', script: 's.ps1' },
    { ...emptyStep('Agent'), id: 'two', name: 'Drugi', agent: 'a' },
    { ...emptyStep('Manual'), id: 'three', name: 'Trzeci' },
  ],
})

async function mountEditor() {
  const wrapper = await mountApp(FlowEditor, { blocks: [] })
  const { useCatalog } = await import('@/stores/catalog')
  await useCatalog().load()
  await flushPromises()
  return wrapper
}

const stepNames = (wrapper: Awaited<ReturnType<typeof mountEditor>>) => wrapper.findAll('.step-title strong').map((title) => title.text())
const click = (wrapper: Awaited<ReturnType<typeof mountEditor>>, selector: string, index = 0) => wrapper.findAll(selector)[index]!.trigger('click')

describe('FlowEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchFlowDocument).mockResolvedValue(document())
  })

  it('loads the steps of the selected task type', async () => {
    const wrapper = await mountEditor()

    expect(fetchFlowDocument).toHaveBeenCalledWith('Bug')
    expect(stepNames(wrapper)).toEqual(['Pierwszy', 'Drugi', 'Trzeci'])
  })

  it('reorders steps with arrows, removes a step and saves that order', async () => {
    vi.mocked(saveFlow).mockResolvedValue({ saved: true, errors: [] })
    const wrapper = await mountEditor()

    await click(wrapper, 'button[title="W dół"]', 0) // Pierwszy za Drugi
    await click(wrapper, 'button[title="Usuń krok"]', 2) // usuń Trzeci
    expect(stepNames(wrapper)).toEqual(['Drugi', 'Pierwszy'])

    await wrapper.findAll('button').find((button) => button.text().includes('Zapisz do YAML'))!.trigger('click')

    expect(vi.mocked(saveFlow).mock.calls[0]![0].flow.map((step) => step.id)).toEqual(['two', 'one'])
  })

  it('shows validation errors from the server and keeps the edited flow', async () => {
    vi.mocked(saveFlow).mockResolvedValue({ saved: false, errors: ["Krok 'one': brak pola 'script' (wymagane dla kind: Script)."] })
    const wrapper = await mountEditor()

    await wrapper.findAll('button').find((button) => button.text().includes('Zapisz do YAML'))!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Flow nie został zapisany')
    expect(wrapper.text()).toContain("brak pola 'script'")
    expect(stepNames(wrapper)).toHaveLength(3)
  })

  it('adds a new step of the chosen kind and opens it for editing', async () => {
    const wrapper = await mountEditor()

    await wrapper.findAll('.add button').find((button) => button.text().includes('Agent'))!.trigger('click')

    expect(stepNames(wrapper)).toHaveLength(4)
    expect(wrapper.text()).toContain('Szablon promptu')
    expect(wrapper.text()).toContain('Budżet tokenów')
  })
})
