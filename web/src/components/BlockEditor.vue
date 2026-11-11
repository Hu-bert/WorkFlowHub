<script setup lang="ts">
import { computed, ref } from 'vue'

import { fetchBlockContent, saveBlockContent, type BlockFile } from '@/api/flowEditor'
import { useNotifications } from '@/stores/notifications'

/** Edytor bloczków: agenci, skille, prompty, skrypty i szablony z katalogu blocks/. */
const props = defineProps<{ blocks: BlockFile[] }>()
const emit = defineEmits<{ changed: [] }>()

const notifications = useNotifications()
const selected = ref<BlockFile | null>(null)
const content = ref('')
const original = ref('')
const saving = ref(false)

const groups = [
  { kind: 'agents', label: 'Agenci', icon: 'mdi-robot-outline' },
  { kind: 'skills', label: 'Skille', icon: 'mdi-school-outline' },
  { kind: 'prompts', label: 'Prompty kroków', icon: 'mdi-message-text-outline' },
  { kind: 'templates', label: 'Szablony', icon: 'mdi-file-document-edit-outline' },
  { kind: 'scripts', label: 'Skrypty', icon: 'mdi-powershell' },
] as const

const blocksOf = (kind: BlockFile['kind']) => props.blocks.filter((block) => block.kind === kind)
const dirty = computed(() => content.value !== original.value)

async function open(block: BlockFile) {
  try {
    const loaded = await fetchBlockContent(block.path)
    selected.value = block
    content.value = original.value = loaded.content
  } catch (error) {
    notifications.error(error)
  }
}

async function save() {
  if (!selected.value) return
  saving.value = true
  try {
    await saveBlockContent({ path: selected.value.path, content: content.value })
    original.value = content.value
    notifications.success(`Zapisano blocks/${selected.value.path}`)
    emit('changed')
  } catch (error) {
    notifications.error(error)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="block-editor">
    <v-card class="list">
      <v-list density="compact" nav>
        <template v-for="group in groups" :key="group.kind">
          <v-list-subheader>{{ group.label }}</v-list-subheader>
          <v-list-item
            v-for="block in blocksOf(group.kind)"
            :key="block.path"
            :active="selected?.path === block.path"
            :prepend-icon="group.icon"
            :title="block.name"
            color="primary"
            rounded="lg"
            @click="open(block)"
          />
        </template>
      </v-list>
    </v-card>

    <v-card v-if="selected" class="content">
      <div class="head">
        <code>blocks/{{ selected.path }}</code>
        <v-btn color="primary" prepend-icon="mdi-content-save-outline" :disabled="!dirty" :loading="saving" @click="save">Zapisz</v-btn>
      </div>
      <v-textarea v-model="content" rows="24" hide-details class="text" spellcheck="false" />
      <p class="fh-muted note">
        Zmiana zapisuje się do pliku w repo FlowHub — sprawdź ją w <code>git diff</code> i zacommituj sam. Do zadań w toku trafi przy
        następnym kroku „Kopiowanie agentów i skilli”.
      </p>
    </v-card>
    <v-card v-else class="content empty fh-muted">Wybierz bloczek z listy, żeby go zobaczyć i poprawić (np. po retro).</v-card>
  </div>
</template>

<style scoped>
.block-editor {
  display: grid;
  grid-template-columns: 300px 1fr;
  gap: 1.5rem;
  align-items: start;
}

.list {
  max-height: 75vh;
  overflow-y: auto;
}

.content {
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.text :deep(textarea) {
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 0.88rem;
  line-height: 1.55;
}

.note {
  margin: 0;
  font-size: 0.8rem;
}

.empty {
  text-align: center;
  padding: 3rem 1rem;
}

@media (max-width: 900px) {
  .block-editor {
    grid-template-columns: 1fr;
  }
}
</style>
