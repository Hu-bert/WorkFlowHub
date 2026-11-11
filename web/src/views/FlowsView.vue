<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { fetchBlocks, type BlockFile } from '@/api/flowEditor'
import BlockEditor from '@/components/BlockEditor.vue'
import FlowEditor from '@/components/FlowEditor.vue'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'

const catalog = useCatalog()
const notifications = useNotifications()
const tab = ref<'flow' | 'blocks'>('flow')
const blocks = ref<BlockFile[]>([])

async function loadBlocks() {
  try {
    blocks.value = await fetchBlocks()
  } catch (error) {
    notifications.error(error)
  }
}

onMounted(() => Promise.all([catalog.load(), loadBlocks()]).catch(notifications.error))
</script>

<template>
  <section class="fh-page flows-page">
    <header class="fh-page-header">
      <div>
        <h1>Flow i bloczki</h1>
        <p>Typy zadań i ich kroki (pliki <code>flows/*.yaml</code>) oraz biblioteka agentów, skilli i promptów (<code>blocks/</code>).</p>
      </div>
    </header>

    <v-alert
      v-for="problem in catalog.flowProblems"
      :key="problem.fileName"
      type="error"
      variant="tonal"
      :title="`Błąd w flows/${problem.fileName}`"
    >
      <div v-for="error in problem.errors" :key="error">{{ error }}</div>
    </v-alert>

    <v-tabs v-model="tab" color="primary">
      <v-tab value="flow" prepend-icon="mdi-sitemap-outline">Flow</v-tab>
      <v-tab value="blocks" prepend-icon="mdi-toy-brick-outline">Bloczki</v-tab>
    </v-tabs>

    <FlowEditor v-if="tab === 'flow'" :blocks="blocks" />
    <BlockEditor v-else :blocks="blocks" @changed="loadBlocks" />
  </section>
</template>

<style scoped>
.flows-page {
  max-width: 1300px;
}
</style>
