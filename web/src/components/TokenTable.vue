<script setup lang="ts">
import { computed } from 'vue'

import type { TokenReportRow } from '@/api/tokens'
import { formatCost, formatTokens } from '@/shared/format'

const props = defineProps<{ title: string; icon: string; labelHeader: string; rows: TokenReportRow[] }>()

/** Pasek przy wierszu pokazuje udział w największej pozycji — widać od razu, co kosztuje najwięcej. */
const max = computed(() => Math.max(1, ...props.rows.map((row) => row.totalTokens)))
</script>

<template>
  <v-card :title="title" :prepend-icon="icon">
    <v-table density="compact">
      <thead>
        <tr>
          <th>{{ labelHeader }}</th>
          <th class="text-right">Tokeny</th>
          <th class="text-right">Koszt</th>
          <th class="text-right">Uruchomień</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in rows" :key="row.label">
          <td>
            <div>{{ row.label }}</div>
            <v-progress-linear :model-value="(row.totalTokens / max) * 100" color="primary" height="4" rounded />
          </td>
          <td class="text-right" :title="`wejście ${row.inputTokens}, wyjście ${row.outputTokens}, cache ${row.cacheReadTokens}`">
            {{ formatTokens(row.totalTokens) }}
          </td>
          <td class="text-right">{{ formatCost(row.cost) }}</td>
          <td class="text-right">{{ row.runs }}</td>
        </tr>
        <tr v-if="rows.length === 0">
          <td colspan="4" class="text-center fh-muted py-4">Brak danych.</td>
        </tr>
      </tbody>
    </v-table>
  </v-card>
</template>
