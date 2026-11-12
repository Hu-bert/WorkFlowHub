<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import type { WorkItemDetails } from '@/api/types'
import NewWorkItemDialog from '@/components/NewWorkItemDialog.vue'
import WorkItemCard from '@/components/WorkItemCard.vue'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'
import { useWorkItems } from '@/stores/workItems'

const catalog = useCatalog()
const workItems = useWorkItems()
const notifications = useNotifications()
const router = useRouter()

const dialogOpen = ref(false)
const dragOverColumn = ref<string | null>(null)

onMounted(async () => {
  try {
    await Promise.all([catalog.load(), workItems.load()])
  } catch (error) {
    notifications.error(error)
  }
})

async function drop(event: DragEvent, column: string) {
  dragOverColumn.value = null
  const id = event.dataTransfer?.getData('text/plain')
  if (!id) return

  try {
    await workItems.move(id, column)
  } catch (error) {
    notifications.error(error)
  }
}

const openItem = (item: WorkItemDetails) => router.push({ name: 'work-item', params: { id: item.summary.id } })
</script>

<template>
  <section class="fh-page kanban-page">
    <header class="fh-page-header">
      <div>
        <h1>Kanban</h1>
        <p>Przeciągnij kartę, żeby zmienić etap pracy. Pomarańczowa ramka = zadanie czeka na Ciebie.</p>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="dialogOpen = true">Nowe zadanie</v-btn>
    </header>

    <div class="board">
      <div
        v-for="column in catalog.columns"
        :key="column.key"
        class="column"
        :class="{ over: dragOverColumn === column.key }"
        @dragover.prevent="dragOverColumn = column.key"
        @dragleave="dragOverColumn = null"
        @drop.prevent="drop($event, column.key)"
      >
        <div class="column-header">
          <v-icon :icon="column.icon" size="20" />
          <span>{{ column.label }}</span>
          <v-chip size="x-small" variant="tonal">{{ workItems.inColumn(column.key).length }}</v-chip>
        </div>

        <div class="cards">
          <WorkItemCard v-for="item in workItems.inColumn(column.key)" :key="item.id" :item="item" />
          <div v-if="workItems.inColumn(column.key).length === 0" class="empty fh-muted">pusto</div>
        </div>
      </div>
    </div>

    <NewWorkItemDialog v-model="dialogOpen" @created="openItem" />
  </section>
</template>

<style scoped>
.kanban-page {
  max-width: none;
}

.board {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: minmax(200px, 1fr);
  gap: 1rem;
  overflow-x: auto;
  padding-bottom: 0.5rem;
  align-items: start;
}

.column {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-height: 60vh;
  padding: 0.75rem;
  border: 1px solid var(--fh-border);
  border-radius: 16px;
  background: rgba(27, 22, 49, 0.55);
  transition:
    border-color 0.15s,
    background 0.15s;
}

.column.over {
  border-color: rgb(var(--v-theme-primary));
  background: rgba(139, 92, 246, 0.12);
}

.column-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  padding: 0.25rem 0.25rem 0;
}

.column-header .v-chip {
  margin-left: auto;
}

.cards {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.empty {
  text-align: center;
  font-size: 0.8rem;
  padding: 1.5rem 0;
  opacity: 0.6;
}
</style>
