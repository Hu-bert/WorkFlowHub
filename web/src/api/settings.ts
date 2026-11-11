import { getJson, postJson, putJson } from './http'

export type SettingKind = 'Text' | 'Secret' | 'Choice' | 'Number' | 'Folder'

export interface SettingField {
  /** Ścieżka w konfiguracji, np. "Agent:Runner". */
  key: string
  label: string
  kind: SettingKind
  hint: string | null
  choices: { value: string; label: string }[] | null
}

export interface SettingValue {
  field: SettingField
  /** Sekrety nigdy nie wracają z serwera — tylko `hasValue`. */
  value: string | null
  hasValue: boolean
}

export interface SettingsModule {
  id: string
  title: string
  icon: string
  description: string
  canTest: boolean
  fields: SettingValue[]
}

export interface ConnectionTest {
  ok: boolean
  message: string
}

export const fetchSettings = () => getJson<SettingsModule[]>('/api/settings')

/** Sekret: null = bez zmian, pusty tekst = usuń. */
export const saveSettings = (moduleId: string, values: Record<string, string | null>) =>
  putJson<SettingsModule>(`/api/settings/${moduleId}`, { values })

export const testSettings = (moduleId: string) => postJson<ConnectionTest>(`/api/settings/${moduleId}/test`)
