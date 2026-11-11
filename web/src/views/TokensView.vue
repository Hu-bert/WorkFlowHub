<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { fetchModels, fetchTokenReport, type ModelsInfo, type TokenReport } from '@/api/tokens'
import StatCard from '@/components/StatCard.vue'
import TokenTable from '@/components/TokenTable.vue'
import { formatCost, formatTokens } from '@/shared/format'
import { useNotifications } from '@/stores/notifications'

const notifications = useNotifications()
const report = ref<TokenReport | null>(null)
const models = ref<ModelsInfo | null>(null)

async function load() {
  try {
    ;[report.value, models.value] = await Promise.all([fetchTokenReport(), fetchModels()])
  } catch (error) {
    notifications.error(error)
  }
}

onMounted(load)

const stats = computed(() => {
  const total = report.value?.total
  return [
    { label: 'Tokeny razem', value: formatTokens(total?.totalTokens ?? 0), icon: 'mdi-lightning-bolt-outline' },
    { label: 'Koszt razem', value: formatCost(total?.cost ?? 0), icon: 'mdi-wallet-outline' },
    { label: 'Uruchomień agenta', value: String(total?.runs ?? 0), icon: 'mdi-robot-outline' },
    {
      label: 'Średnio na uruchomienie',
      value: formatTokens(total?.runs ? Math.round(total.totalTokens / total.runs) : 0),
      icon: 'mdi-scale-balance',
    },
  ]
})

const maxDay = computed(() => Math.max(1, ...(report.value?.byDay.map((day) => day.totalTokens) ?? [])))
</script>

<template>
  <section class="fh-page">
    <header class="fh-page-header">
      <div>
        <h1>Tokeny i koszt</h1>
        <p>Gdzie idą tokeny: per model, typ zadania, zadanie i krok. Koszt liczony z cennika w konfiguracji.</p>
      </div>
      <v-btn variant="tonal" color="primary" prepend-icon="mdi-refresh" @click="load">Odśwież</v-btn>
    </header>

    <div class="stats">
      <StatCard v-for="stat in stats" :key="stat.label" v-bind="stat" />
    </div>

    <v-card v-if="report" title="Trend dzienny" prepend-icon="mdi-chart-bar">
      <v-card-text>
        <p v-if="report.byDay.length === 0" class="fh-muted mb-0">Brak danych — uruchom krok agenta.</p>
        <div v-else class="trend">
          <div v-for="day in report.byDay" :key="day.label" class="day" :title="`${formatTokens(day.totalTokens)} · ${formatCost(day.cost)}`">
            <span class="value">{{ formatTokens(day.totalTokens) }}</span>
            <div class="bar" :style="{ height: `${Math.max(4, (day.totalTokens / maxDay) * 140)}px` }" />
            <span class="fh-muted label">{{ day.label.slice(5) }}</span>
          </div>
        </div>
      </v-card-text>
    </v-card>

    <div v-if="report" class="tables">
      <TokenTable title="Per model" icon="mdi-brain" label-header="Model" :rows="report.byModel" />
      <TokenTable title="Per typ zadania" icon="mdi-shape-outline" label-header="Typ" :rows="report.byType" />
      <TokenTable title="Per krok" icon="mdi-debug-step-over" label-header="Krok" :rows="report.byStep" />
      <TokenTable title="Per zadanie" icon="mdi-clipboard-text-outline" label-header="Zadanie" :rows="report.byWorkItem" />
    </div>

    <v-card v-if="models" title="Routing modeli" prepend-icon="mdi-routes">
      <v-card-text>
        <p class="fh-muted">
          Agent: <strong>{{ models.agentRunner }}</strong> · poziom domyślny: <strong>{{ models.defaultTier }}</strong> ·
          budżet zadania:
          <strong>{{ models.taskTokenBudget ? formatTokens(models.taskTokenBudget) : 'bez limitu' }}</strong>
          <template v-if="models.unlimited"> · plan bez limitu (koszt nie jest liczony)</template>. Zmiana w
          <RouterLink to="/settings">Ustawieniach</RouterLink>.
        </p>
        <v-chip v-for="tier in models.tiers" :key="tier.tier" class="mr-2" variant="tonal" color="primary">
          tier:{{ tier.tier }} → {{ tier.model }}
        </v-chip>
      </v-card-text>
    </v-card>
  </section>
</template>

<style scoped>
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.tables {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(420px, 1fr));
  gap: 1.5rem;
}

.trend {
  display: flex;
  align-items: flex-end;
  gap: 0.75rem;
  min-height: 190px;
  overflow-x: auto;
}

.day {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.3rem;
  min-width: 56px;
}

.bar {
  width: 32px;
  border-radius: 8px 8px 2px 2px;
  background: var(--fh-accent-gradient);
}

.value {
  font-size: 0.75rem;
}

.label {
  font-size: 0.75rem;
}
</style>
