import { defineStore } from 'pinia'
import { ref } from 'vue'

import { fetchHealth } from '@/api/health'
import { createLiveConnection } from '@/api/live'
import type { ConnectionState } from '@/shared/connectionState'

/** Stan połączeń aplikacji z backendem (REST + SignalR). */
export const useSystemStore = defineStore('system', () => {
  const api = ref<ConnectionState>('checking')
  const live = ref<ConnectionState>('checking')

  async function checkApi() {
    try {
      await fetchHealth()
      api.value = 'online'
    } catch {
      api.value = 'offline'
    }
  }

  async function connectLive() {
    const connection = createLiveConnection()
    connection.onreconnecting(() => (live.value = 'checking'))
    connection.onreconnected(() => (live.value = 'online'))
    connection.onclose(() => (live.value = 'offline'))

    try {
      await connection.start()
      live.value = 'online'
    } catch {
      live.value = 'offline'
    }
  }

  return { api, live, checkApi, connectLive }
})
