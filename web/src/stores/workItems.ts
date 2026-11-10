import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import type { CreateWorkItemRequest, WorkItemSummary } from '@/api/types'
import { createWorkItem, fetchWorkItems, moveWorkItem } from '@/api/workItems'

/** Lista zadań (dashboard i kanban). */
export const useWorkItems = defineStore('workItems', () => {
  const items = ref<WorkItemSummary[]>([])
  const loading = ref(false)

  const waitingForUser = computed(() => items.value.filter((item) => item.waitsForUser))
  const inColumn = (column: string) => items.value.filter((item) => item.boardColumn === column)

  async function load() {
    loading.value = true
    try {
      items.value = await fetchWorkItems()
    } finally {
      loading.value = false
    }
  }

  async function create(request: CreateWorkItemRequest) {
    const created = await createWorkItem(request)
    items.value.unshift(created.summary)
    return created
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

  return { items, loading, waitingForUser, inColumn, load, create, move }
})
