import { flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { saveSettings, testSettings, type SettingsModule } from '@/api/settings'
import { mountApp } from '@/test/mountApp'

import SettingsModuleCard from '../SettingsModuleCard.vue'

vi.mock('@/api/settings', () => ({ saveSettings: vi.fn(), testSettings: vi.fn() }))

const module = (): SettingsModule => ({
  id: 'tfs',
  title: 'TFS',
  icon: 'mdi-source-pull',
  description: 'Pull requesty',
  canTest: true,
  fields: [
    { field: { key: 'Tfs:CollectionUrl', label: 'Adres kolekcji', kind: 'Text', hint: null, choices: null }, value: 'https://stary', hasValue: true },
    { field: { key: 'Tfs:Token', label: 'Token (PAT)', kind: 'Secret', hint: null, choices: null }, value: null, hasValue: true },
  ],
})

const button = (wrapper: Awaited<ReturnType<typeof mountApp>>, text: string) =>
  wrapper.findAll('button').find((candidate) => candidate.text().includes(text))!

describe('SettingsModuleCard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(saveSettings).mockResolvedValue(module())
  })

  it('never shows a saved token, only that it is set', async () => {
    const wrapper = await mountApp(SettingsModuleCard, { module: module(), status: null })

    const token = wrapper.find('input[type="password"]')
    expect((token.element as HTMLInputElement).value).toBe('')
    expect(wrapper.text()).toContain('Token zapisany')
  })

  it('saves only the changed fields, leaves an untouched token alone and then tests the connection', async () => {
    const wrapper = await mountApp(SettingsModuleCard, { module: module(), status: null })

    await wrapper.find('input[type="text"]').setValue('https://nowy')
    await button(wrapper, 'Zapisz').trigger('click')
    await flushPromises()

    expect(saveSettings).toHaveBeenCalledWith('tfs', { 'Tfs:CollectionUrl': 'https://nowy' })
    expect(wrapper.emitted('saved')).toHaveLength(1)
  })

  it('shows the result of the connection test in words', async () => {
    vi.mocked(testSettings).mockResolvedValue({ ok: false, message: 'zły albo wygasły token' })
    const wrapper = await mountApp(SettingsModuleCard, { module: module(), status: null })

    await button(wrapper, 'Sprawdź połączenie').trigger('click')
    await flushPromises()

    expect(saveSettings).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Połączenie nie działa')
    expect(wrapper.text()).toContain('zły albo wygasły token')
  })
})
