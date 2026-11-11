<script setup lang="ts">
import { onMounted } from 'vue'

import StatusBadge from '@/components/StatusBadge.vue'
import { connectionPresentation } from '@/shared/connectionState'
import { connectionPresentationOf, useConnections } from '@/stores/connections'
import { useSystemStore } from '@/stores/system'

import { navigation } from './navigation'

const system = useSystemStore()
const connections = useConnections()

// Stan integracji widać z każdej strony; szczegóły i poprawki są w Ustawieniach.
onMounted(() => connections.load())
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
      <RouterLink v-if="connections.statuses.length" to="/settings" class="connections integrations" title="Stan połączeń — kliknij, żeby przejść do Ustawień">
        <div class="caption">Integracje</div>
        <div v-for="status in connections.statuses" :key="status.id" class="connection" :title="status.message">
          <span class="integration-name">{{ status.title.split(' / ')[0] }}</span>
          <span class="state" :class="status.state.toLowerCase()">
            <v-icon :icon="connectionPresentationOf[status.state].icon" size="14" />
            {{ connectionPresentationOf[status.state].label }}
          </span>
        </div>
      </RouterLink>

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

.integrations {
  margin-bottom: 0;
  text-decoration: none;
  gap: 0.35rem;
}

.integrations:hover {
  border-color: rgba(167, 139, 250, 0.45);
}

.caption {
  font-size: 0.7rem;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  opacity: 0.7;
}

.integration-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.8rem;
}

.state {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  flex: none;
  font-size: 0.75rem;
}

.state.ok {
  color: #4ade80;
}

.state.failed {
  color: #f87171;
}

.connection {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
