<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import type { WorkItemDetails } from '@/api/types'
import NewWorkItemDialog from '@/components/NewWorkItemDialog.vue'
import StatCard from '@/components/StatCard.vue'
import WorkItemCard from '@/components/WorkItemCard.vue'
import { formatCost, formatTokens } from '@/shared/format'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'
import { useWorkItems } from '@/stores/workItems'

const catalog = useCatalog()
const workItems = useWorkItems()
const notifications = useNotifications()
const router = useRouter()
const dialogOpen = ref(false)

const active = computed(() => workItems.items.filter((item) => item.stepsDone < item.stepsTotal))
const runningWithoutUser = computed(() => active.value.filter((item) => !item.waitsForUser))
const totalTokens = computed(() => workItems.items.reduce((sum, item) => sum + item.usage.totalTokens, 0))
const totalCost = computed(() => workItems.items.reduce((sum, item) => sum + item.usage.cost, 0))

const stats = computed(() => [
  { label: 'W toku', value: String(active.value.length), icon: 'mdi-play-circle-outline' },
  {
    label: 'Czeka na Ciebie',
    value: String(workItems.waitingForUser.length),
    icon: 'mdi-account-clock-outline',
    hint: 'kroki do akceptacji',
  },
  { label: 'Tokeny', value: formatTokens(totalTokens.value), icon: 'mdi-lightning-bolt-outline', hint: 'wszystkie zadania' },
  { label: 'Koszt', value: formatCost(totalCost.value), icon: 'mdi-wallet-outline', hint: 'wszystkie zadania' },
])

onMounted(async () => {
  try {
    await Promise.all([catalog.load(), workItems.load()])
  } catch (error) {
    notifications.error(error)
  }
})

const openItem = (item: WorkItemDetails) => router.push({ name: 'work-item', params: { id: item.summary.id } })
</script>

<template>
  <section class="fh-page">
    <header class="fh-page-header">
      <div>
        <h1>Dashboard</h1>
        <p>Twoje zadania, przepływy i koszty w jednym miejscu.</p>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="dialogOpen = true">Nowe zadanie</v-btn>
    </header>

    <v-alert
      v-for="problem in catalog.flowProblems"
      :key="problem.fileName"
      type="error"
      variant="tonal"
      :title="`Błąd w flows/${problem.fileName}`"
    >
      <div v-for="error in problem.errors" :key="error">{{ error }}</div>
    </v-alert>

    <div class="stats">
      <StatCard v-for="stat in stats" :key="stat.label" v-bind="stat" />
    </div>

    <template v-if="workItems.waitingForUser.length">
      <h2 class="section-title">Czeka na Ciebie</h2>
      <div class="cards">
        <WorkItemCard v-for="item in workItems.waitingForUser" :key="item.id" :item="item" />
      </div>
    </template>

    <template v-if="runningWithoutUser.length">
      <h2 class="section-title">W toku</h2>
      <div class="cards">
        <WorkItemCard v-for="item in runningWithoutUser" :key="item.id" :item="item" />
      </div>
    </template>

    <v-card v-if="workItems.items.length === 0 && !workItems.loading" class="empty">
      <v-icon icon="mdi-inbox-outline" size="48" color="primary" />
      <h2>Brak zadań</h2>
      <p class="fh-muted">Utwórz pierwsze zadanie — FlowHub poprowadzi je krok po kroku.</p>
    </v-card>

    <NewWorkItemDialog v-model="dialogOpen" @created="openItem" />
  </section>
</template>

<style scoped>
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.section-title {
  margin: 0.5rem 0 -0.5rem;
  font-size: 1.1rem;
  font-weight: 600;
}

.empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.4rem;
  padding: 3.5rem 1rem;
  text-align: center;
}

.empty h2 {
  margin: 0.5rem 0 0;
  font-size: 1.25rem;
}

.empty p {
  margin: 0;
}
</style>
