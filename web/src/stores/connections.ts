import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { fetchConnectionStatus, type ConnectionStatus } from '@/api/settings'

export const connectionPresentationOf = {
  Fake: { label: 'Atrapa', color: 'grey', icon: 'mdi-flask-outline' },
  Ok: { label: 'Działa', color: 'success', icon: 'mdi-check-circle' },
  Failed: { label: 'Nie działa', color: 'error', icon: 'mdi-close-circle' },
  NotConfigured: { label: 'Nie ustawiono', color: 'grey', icon: 'mdi-minus-circle-outline' },
} as const

/** Stan połączeń z Copilotem i systemami firmowymi — wspólny dla paska bocznego i Ustawień. */
export const useConnections = defineStore('connections', () => {
  const statuses = ref<ConnectionStatus[]>([])
  const loading = ref(false)

  const byId = computed(() => new Map(statuses.value.map((status) => [status.id, status])))
  const failed = computed(() => statuses.value.filter((status) => status.state === 'Failed'))

  /** Błąd pobrania statusu nie może psuć reszty aplikacji — pasek po prostu zostaje pusty. */
  async function load(refresh = false) {
    loading.value = true
    try {
      statuses.value = await fetchConnectionStatus(refresh)
    } catch {
      statuses.value = []
    } finally {
      loading.value = false
    }
  }

  return { statuses, loading, byId, failed, load }
})