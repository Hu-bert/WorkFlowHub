<script setup lang="ts">
import type { StatusCell } from '@/api/integrations'
import { healthPresentation } from '@/shared/healthPresentation'

/** Komórka tabeli repo: ikona + kolor + tekst, a gdy jest adres — link do systemu źródłowego. */
defineProps<{ cell: StatusCell }>()
</script>

<template>
  <component
    :is="cell.url ? 'a' : 'span'"
    class="health-cell"
    :href="cell.url ?? undefined"
    target="_blank"
    rel="noopener"
    :title="healthPresentation[cell.level].label"
    @click.stop
  >
    <v-icon :icon="healthPresentation[cell.level].icon" :color="healthPresentation[cell.level].color" size="18" />
    <span>{{ cell.text }}</span>
  </component>
</template>

<style scoped>
.health-cell {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  color: inherit;
  text-decoration: none;
  white-space: nowrap;
}

a.health-cell:hover span {
  text-decoration: underline;
}
</style>
