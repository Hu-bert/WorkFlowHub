<script setup lang="ts">
import StatusBadge from '@/components/StatusBadge.vue'
import { connectionPresentation } from '@/shared/connectionState'
import { useSystemStore } from '@/stores/system'

import { navigation } from './navigation'

const system = useSystemStore()
</script>

<template>
  <aside class="sidebar">
    <div class="brand">
      <span class="logo"><i class="pi pi-bolt" /></span>
      <span class="name">FlowHub</span>
    </div>

    <nav class="nav">
      <template v-for="item in navigation" :key="item.to">
        <span v-if="item.comingInPhase" class="nav-item disabled" :title="`Faza ${item.comingInPhase}`">
          <i :class="item.icon" />
          {{ item.label }}
          <small>wkrótce</small>
        </span>
        <RouterLink v-else :to="item.to" class="nav-item" active-class="active">
          <i :class="item.icon" />
          {{ item.label }}
        </RouterLink>
      </template>
    </nav>

    <div class="connections">
      <div class="connection">
        <span>API</span>
        <StatusBadge v-bind="connectionPresentation[system.api]" />
      </div>
      <div class="connection">
        <span>Na żywo</span>
        <StatusBadge v-bind="connectionPresentation[system.live]" />
      </div>
    </div>
  </aside>
</template>

<style scoped>
.sidebar {
  display: flex;
  flex-direction: column;
  gap: 2rem;
  padding: 1.5rem 1rem;
  background: rgba(24, 20, 43, 0.85);
  border-right: 1px solid var(--fh-border);
  backdrop-filter: blur(8px);
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0 0.5rem;
}

.logo {
  display: grid;
  place-items: center;
  width: 38px;
  height: 38px;
  border-radius: 11px;
  background: var(--fh-accent-gradient);
  box-shadow: 0 6px 20px rgba(139, 92, 246, 0.35);
}

.name {
  font-size: 1.25rem;
  font-weight: 700;
  letter-spacing: 0.01em;
}

.nav {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.7rem 0.85rem;
  border-radius: 10px;
  color: var(--fh-muted);
  font-weight: 500;
  transition:
    background 0.15s,
    color 0.15s;
}

.nav-item:hover:not(.disabled) {
  background: rgba(139, 92, 246, 0.1);
  color: var(--fh-text);
}

.nav-item.active {
  background: rgba(139, 92, 246, 0.18);
  color: var(--fh-text);
  box-shadow: inset 3px 0 0 var(--fh-accent);
}

.nav-item.disabled {
  opacity: 0.45;
  cursor: default;
}

.nav-item small {
  margin-left: auto;
  font-size: 0.7rem;
}

.connections {
  margin-top: auto;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  padding: 0.85rem;
  border: 1px solid var(--fh-border);
  border-radius: 12px;
  font-size: 0.85rem;
  color: var(--fh-muted);
}

.connection {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
