<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'

const props = defineProps<{ text: string; running?: boolean }>()
const box = ref<HTMLElement | null>(null)

// Konsola sama przewija się na dół, gdy dochodzi nowe wyjście.
watch(
  () => props.text,
  async () => {
    await nextTick()
    if (box.value) box.value.scrollTop = box.value.scrollHeight
  },
  { immediate: true },
)
</script>

<template>
  <pre ref="box" class="console">{{ text || (running ? 'Czekam na wyjście…' : 'Brak wyjścia.') }}<span v-if="running" class="cursor">▍</span></pre>
</template>

<style scoped>
.console {
  margin: 0;
  padding: 1rem;
  max-height: 420px;
  min-height: 120px;
  overflow: auto;
  border: 1px solid var(--fh-border);
  border-radius: 12px;
  background: #0b0916;
  color: #d9d3f5;
  font-family: 'Cascadia Code', Consolas, 'Courier New', monospace;
  font-size: 0.85rem;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
}

.cursor {
  color: #a78bfa;
  animation: blink 1s steps(2) infinite;
}

@keyframes blink {
  50% {
    opacity: 0;
  }
}
</style>
