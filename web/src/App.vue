<script setup lang="ts">
import { onMounted } from 'vue'

import AppLayout from '@/layout/AppLayout.vue'
import { useSystemStore } from '@/stores/system'

const system = useSystemStore()

onMounted(() => {
  void system.checkApi()
  void system.connectLive()
})
</script>

<template>
  <AppLayout>
    <RouterView />
  </AppLayout>
</template>
