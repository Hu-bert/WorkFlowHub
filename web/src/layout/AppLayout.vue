<script setup lang="ts">
import { computed } from 'vue'

import { useNotifications } from '@/stores/notifications'

import AppSidebar from './AppSidebar.vue'

const notifications = useNotifications()

const snackbarOpen = computed({
  get: () => notifications.current !== null,
  set: (open) => {
    if (!open) notifications.clear()
  },
})
</script>

<template>
  <v-app>
    <AppSidebar />
    <v-main>
      <div class="content">
        <slot />
      </div>
    </v-main>

    <v-snackbar v-model="snackbarOpen" :color="notifications.current?.color" timeout="6000" location="bottom right">
      {{ notifications.current?.text }}
    </v-snackbar>
  </v-app>
</template>

<style scoped>
.content {
  padding: 2rem 2.5rem;
}

@media (max-width: 800px) {
  .content {
    padding: 1.25rem 1rem;
  }
}
</style>
