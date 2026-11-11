import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { fetchRepoLevels, type RepoLevel } from '@/api/integrations'
import type { CreateWorkItemRequest, WorkItemSummary } from '@/api/types'
import { createWorkItem, fetchWorkItems, moveWorkItem } from '@/api/workItems'

/** Lista zadań (dashboard i kanban). */
export const useWorkItems = defineStore('workItems', () => {
  const items = ref<WorkItemSummary[]>([])
  const loading = ref(false)

  const waitingForUser = computed(() => items.value.filter((item) => item.waitsForUser))
  const inColumn = (column: string) => items.value.filter((item) => item.boardColumn === column)

  /** Kropki statusu per repo (PR / build / wdrożenia) dla kart zadań. */
  const repoLevels = ref<Record<string, RepoLevel[]>>({})

  async function load() {
    loading.value = true
    try {
      items.value = await fetchWorkItems()
      // Kropki są dodatkiem — gdy integracje nie odpowiadają, lista zadań i tak ma się pokazać.
      repoLevels.value = await fetchRepoLevels().catch(() => ({}))
    } finally {
      loading.value = false
    }
  }

  async function create(request: CreateWorkItemRequest) {
    const created = await createWorkItem(request)
    items.value.unshift(created.summary)
    return created
  }

  /** Podmienia zadanie na liście (zmiana przyszła na żywo z backendu). */
  function replace(summary: WorkItemSummary) {
    const index = items.value.findIndex((candidate) => candidate.id === summary.id)
    if (index >= 0) items.value[index] = summary
  }

  /** Przenosi kartę od razu w UI; gdy API odmówi — cofa. */
  async function move(id: string, column: string) {
    const item = items.value.find((candidate) => candidate.id === id)
    if (!item || item.boardColumn === column) return

    const previous = item.boardColumn
    item.boardColumn = column
    try {
      await moveWorkItem(id, column)
    } catch (error) {
      item.boardColumn = previous
      throw error
    }
  }

  return { items, loading, repoLevels, waitingForUser, inColumn, load, create, replace, move }
})
