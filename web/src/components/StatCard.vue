<script setup lang="ts">
defineProps<{
  label: string
  value: string
  icon: string
  hint?: string
}>()
</script>

<template>
  <div class="fh-card stat">
    <span class="icon"><i :class="icon" /></span>
    <div>
      <div class="label">{{ label }}</div>
      <div class="value">{{ value }}</div>
      <div v-if="hint" class="hint">{{ hint }}</div>
    </div>
  </div>
</template>

<style scoped>
.stat {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.icon {
  display: grid;
  place-items: center;
  flex: none;
  width: 48px;
  height: 48px;
  border-radius: 12px;
  font-size: 1.25rem;
  color: #c4b5fd;
  background: rgba(139, 92, 246, 0.16);
}

.label {
  color: var(--fh-muted);
  font-size: 0.85rem;
}

.value {
  font-size: 1.75rem;
  font-weight: 700;
  line-height: 1.2;
}

.hint {
  color: var(--fh-muted);
  font-size: 0.75rem;
}
</style>
