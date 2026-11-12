import { getJson, postJson, putJson } from './http'

export type SettingKind = 'Text' | 'Secret' | 'Choice' | 'Number' | 'Folder'

export interface SettingField {
  /** Ścieżka w konfiguracji, np. "Agent:Runner". */
  key: string
  label: string
  kind: SettingKind
  hint: string | null
  choices: { value: string; label: string }[] | null
  /** Pole ma listę podpowiedzi z serwera (nadal można wpisać własną wartość). */
  suggest: boolean
  /** Wiele wartości, zapisywanych po przecinku. */
  multiple: boolean
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
  /** false = moduł nie jest jeszcze ustawiony (to nie błąd). */
  configured?: boolean
}

export const fetchSettings = () => getJson<SettingsModule[]>('/api/settings')

/** Sekret: null = bez zmian, pusty tekst = usuń. */
export const saveSettings = (moduleId: string, values: Record<string, string | null>) =>
  putJson<SettingsModule>(`/api/settings/${moduleId}`, { values })

export const testSettings = (moduleId: string) => postJson<ConnectionTest>(`/api/settings/${moduleId}/test`)

/** Stan połączenia: Fake = atrapa (dane przykładowe), Ok = działa, Failed = nie działa. */
export interface ConnectionStatus {
  id: string
  title: string
  icon: string
  state: 'Fake' | 'Ok' | 'Failed' | 'NotConfigured'
  message: string
  checkedAt: string | null
}

/** refresh = sprawdź teraz (bez tego serwer oddaje wynik sprzed najwyżej kilku minut). */
export const fetchConnectionStatus = (refresh = false) => getJson<ConnectionStatus[]>(`/api/settings/status?refresh=${refresh}`)

export const fetchSuggestions = (moduleId: string, key: string) =>
  getJson<string[]>(`/api/settings/${moduleId}/suggestions?key=${encodeURIComponent(key)}`)

/** Miejsce na dysku, w którym FlowHub trzyma dane. */
export interface StorageLocation {
  name: string
  path: string
  description: string
}

export const fetchStorage = () => getJson<StorageLocation[]>('/api/settings/storage')
