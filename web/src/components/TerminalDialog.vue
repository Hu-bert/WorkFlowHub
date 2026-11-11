<script setup lang="ts">
import '@xterm/xterm/css/xterm.css'

import type { FitAddon } from '@xterm/addon-fit'
import type { Terminal } from '@xterm/xterm'
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'

import { checkTerminal, terminalUrl, type TerminalTarget } from '@/api/terminal'
import { useNotifications } from '@/stores/notifications'

/**
 * Interaktywny Copilot CLI w oknie aplikacji (xterm.js ↔ WebSocket ↔ pseudokonsola na serwerze).
 * O zgodę na narzędzia Copilot pyta tutaj, w terminalu — tak jak w zwykłej konsoli.
 */
const props = defineProps<{ target: TerminalTarget | null; title: string }>()
const emit = defineEmits<{ close: [] }>()

const notifications = useNotifications()
const screen = ref<HTMLElement | null>(null)
const state = ref<'connecting' | 'open' | 'closed'>('connecting')

let terminal: Terminal | null = null
let fit: FitAddon | null = null
let socket: WebSocket | null = null
let observer: ResizeObserver | null = null

const send = (message: object) => socket?.readyState === WebSocket.OPEN && socket.send(JSON.stringify(message))

async function open(target: TerminalTarget) {
  state.value = 'connecting'
  try {
    // Najpierw zwykłe zapytanie — błąd (np. brak katalogu) dostajemy jako czytelny komunikat.
    await checkTerminal(target)
  } catch (error) {
    notifications.error(error)
    emit('close')
    return
  }

  const [{ Terminal }, { FitAddon }] = await Promise.all([import('@xterm/xterm'), import('@xterm/addon-fit')])
  await nextTick()
  if (!screen.value) return

  terminal = new Terminal({
    cursorBlink: true,
    fontFamily: "'Cascadia Code', Consolas, monospace",
    fontSize: 14,
    theme: { background: '#120f24', foreground: '#ece8ff', cursor: '#c4b5fd', selectionBackground: '#4c3d8f' },
  })
  fit = new FitAddon()
  terminal.loadAddon(fit)
  terminal.open(screen.value)
  fit.fit()

  socket = new WebSocket(terminalUrl(target, terminal.cols, terminal.rows))
  socket.binaryType = 'arraybuffer'
  socket.onopen = () => {
    state.value = 'open'
    // Okno mogło zmienić rozmiar, zanim połączenie się otworzyło — wyrównujemy rozmiar konsoli z ekranem.
    fit?.fit()
    if (terminal) send({ type: 'resize', cols: terminal.cols, rows: terminal.rows })
    terminal?.focus()
  }
  socket.onmessage = (event) => terminal?.write(new Uint8Array(event.data as ArrayBuffer))
  socket.onclose = () => {
    state.value = 'closed'
    terminal?.write('\r\n\x1b[38;5;141m— sesja zakończona —\x1b[0m\r\n')
  }

  terminal.onData((data) => send({ type: 'input', data }))
  terminal.onResize(({ cols, rows }) => send({ type: 'resize', cols, rows }))

  observer = new ResizeObserver(() => fit?.fit())
  observer.observe(screen.value)
}

function dispose() {
  observer?.disconnect()
  socket?.close()
  terminal?.dispose()
  observer = null
  socket = null
  terminal = null
  fit = null
}

watch(
  () => props.target,
  (target) => {
    dispose()
    if (target) void open(target)
  },
  { immediate: true },
)

onBeforeUnmount(dispose)
</script>

<template>
  <v-dialog :model-value="target !== null" fullscreen persistent transition="dialog-bottom-transition">
    <v-card class="terminal-card">
      <header class="bar">
        <v-icon icon="mdi-console" />
        <strong>{{ title }}</strong>
        <v-chip size="small" :color="state === 'open' ? 'success' : state === 'closed' ? 'grey' : 'warning'" variant="tonal">
          {{ state === 'open' ? 'Połączono' : state === 'closed' ? 'Zakończono' : 'Łączenie…' }}
        </v-chip>
        <span class="fh-muted hint">Copilot pyta tutaj o zgodę na polecenia i zapis plików. Zamknięcie okna kończy sesję.</span>
        <v-spacer />
        <v-btn variant="tonal" prepend-icon="mdi-close" @click="emit('close')">Zamknij</v-btn>
      </header>
      <div class="frame"><div ref="screen" class="screen" /></div>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.terminal-card {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: #120f24 !important;
}

.bar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--fh-border);
}

.hint {
  font-size: 0.85rem;
}

/* Odstęp jest na ramce, nie na ekranie — inaczej xterm źle liczy liczbę wierszy i ucina ostatni. */
.frame {
  flex: 1;
  min-height: 0;
  padding: 0.5rem 0.75rem;
}

.screen {
  width: 100%;
  height: 100%;
}
</style>
