<script setup lang="ts">
defineProps<{
  label: string
  value: string
  icon: string
  hint?: string
}>()
</script>

<template>
  <v-card class="stat">
    <span class="icon"><v-icon :icon="icon" /></span>
    <div>
      <div class="fh-muted label">{{ label }}</div>
      <div class="value">{{ value }}</div>
      <div v-if="hint" class="fh-muted hint">{{ hint }}</div>
    </div>
  </v-card>
</template>

<style scoped>
.stat {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
}

.icon {
  display: grid;
  place-items: center;
  flex: none;
  width: 48px;
  height: 48px;
  border-radius: 12px;
  color: #c4b5fd;
  background: rgba(139, 92, 246, 0.16);
}

.label {
  font-size: 0.85rem;
}

.value {
  font-size: 1.75rem;
  font-weight: 700;
  line-height: 1.2;
}

.hint {
  font-size: 0.75rem;
}
</style>
