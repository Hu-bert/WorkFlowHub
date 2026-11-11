<script setup lang="ts">
import StatusBadge from '@/components/StatusBadge.vue'
import { connectionPresentation } from '@/shared/connectionState'
import { useSystemStore } from '@/stores/system'

import { navigation } from './navigation'

const system = useSystemStore()
</script>

<template>
  <v-navigation-drawer permanent width="248" class="sidebar">
    <div class="brand">
      <span class="logo"><v-icon icon="mdi-lightning-bolt" /></span>
      <span class="name">FlowHub</span>
    </div>

    <v-list nav density="comfortable" color="primary">
      <v-list-item
        v-for="item in navigation"
        :key="item.to"
        :to="item.to"
        :prepend-icon="item.icon"
        :title="item.label"
        rounded="lg"
      />
    </v-list>

    <template #append>
      <div class="connections">
        <div class="connection">
          <span>API</span>
          <StatusBadge v-bind="connectionPresentation[system.api]" size="small" />
        </div>
        <div class="connection">
          <span>Na żywo</span>
          <StatusBadge v-bind="connectionPresentation[system.live]" size="small" />
        </div>
      </div>
    </template>
  </v-navigation-drawer>
</template>

<style scoped>
.sidebar {
  background: rgba(24, 20, 43, 0.9) !important;
  border-right: 1px solid var(--fh-border) !important;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1.5rem 1.25rem 1.25rem;
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
}

.connections {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  margin: 1rem;
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
