import { defineStore } from 'pinia'
import { ref } from 'vue'

import { fetchBranchStatus, fetchLocalRepositories, type CachedBranchStatus, type LocalRepository, type LocalRepositoryScan } from '@/api/repositories'

import { useNotifications } from './notifications'

/**
 * Lokalne kopie repo i ich statusy. Trzymane w store, więc powrót na stronę Repozytoria niczego nie ładuje od nowa;
 * serwer i tak pamięta wynik przez czas ustawiony w Ustawieniach. „Odśwież” czyta wszystko od razu.
 */
export const useLocalRepositories = defineStore('localRepositories', () => {
  const scan = ref<LocalRepositoryScan | null>(null)
  const statuses = ref<Record<string, CachedBranchStatus>>({})
  const loading = ref(false)
  const notifications = useNotifications()

  async function load(refresh = false) {
    if (scan.value && !refresh) return

    loading.value = true
    try {
      scan.value = await fetchLocalRepositories(refresh)
      if (refresh) statuses.value = {}
      // Statusy doczytujemy osobno dla każdego repo — jedno wolne połączenie nie blokuje całej listy.
      await Promise.all(scan.value.repositories.filter((repository) => repository.catalogName).map((repository) => loadStatus(repository, refresh)))
    } catch (error) {
      notifications.error(error)
    } finally {
      loading.value = false
    }
  }

  async function loadStatus(repository: LocalRepository, refresh = false) {
    try {
      statuses.value[repository.path] = await fetchBranchStatus(repository.catalogName!, repository.branch, refresh)
    } catch (error) {
      notifications.error(error)
    }
  }

  return { scan, statuses, loading, load, loadStatus }
})