<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'

import { saveSettings, testSettings, type ConnectionTest, type SettingsModule } from '@/api/settings'
import { useNotifications } from '@/stores/notifications'

/** Jedna karta ustawień. Pola rysują się z opisu modułu z serwera — nowy moduł nie wymaga zmian w UI. */
const props = defineProps<{ module: SettingsModule }>()
const emit = defineEmits<{ saved: [module: SettingsModule] }>()

const notifications = useNotifications()
const values = reactive<Record<string, string>>({})
const saving = ref(false)
const testing = ref(false)
const test = ref<ConnectionTest | null>(null)

watch(
  () => props.module,
  (module) => {
    for (const item of module.fields) {
      values[item.field.key] = item.field.kind === 'Secret' ? '' : (item.value ?? '')
    }
  },
  { immediate: true },
)

const dirty = computed(() =>
  props.module.fields.some((item) =>
    item.field.kind === 'Secret' ? values[item.field.key] !== '' : values[item.field.key] !== (item.value ?? ''),
  ),
)

/** Wysyłamy tylko zmienione pola; pusty sekret = „bez zmian”. */
function changes() {
  const changed: Record<string, string | null> = {}
  for (const item of props.module.fields) {
    const value = values[item.field.key] ?? ''
    if (item.field.kind === 'Secret') {
      if (value !== '') changed[item.field.key] = value
    } else if (value !== (item.value ?? '')) {
      changed[item.field.key] = String(value)
    }
  }
  return changed
}

async function save() {
  saving.value = true
  try {
    emit('saved', await saveSettings(props.module.id, changes()))
    notifications.success(`Zapisano: ${props.module.title}`)
    return true
  } catch (error) {
    notifications.error(error)
    return false
  } finally {
    saving.value = false
  }
}

async function clearSecret(key: string) {
  try {
    emit('saved', await saveSettings(props.module.id, { [key]: '' }))
    notifications.success('Usunięto zapisany token')
  } catch (error) {
    notifications.error(error)
  }
}

/** Test sprawdza zapisane ustawienia, więc niezapisane zmiany najpierw zapisujemy. */
async function runTest() {
  if (dirty.value && !(await save())) return

  testing.value = true
  test.value = null
  try {
    test.value = await testSettings(props.module.id)
  } catch (error) {
    notifications.error(error)
  } finally {
    testing.value = false
  }
}
</script>

<template>
  <v-card class="module">
    <header class="head">
      <span class="icon"><v-icon :icon="module.icon" /></span>
      <div>
        <h2>{{ module.title }}</h2>
        <p class="fh-muted">{{ module.description }}</p>
      </div>
    </header>

    <template v-for="item in module.fields" :key="item.field.key">
      <v-select
        v-if="item.field.kind === 'Choice'"
        v-model="values[item.field.key]"
        :items="item.field.choices ?? []"
        item-title="label"
        item-value="value"
        :label="item.field.label"
        :hint="item.field.hint ?? undefined"
        :persistent-hint="!!item.field.hint"
      />
      <v-text-field
        v-else-if="item.field.kind === 'Secret'"
        v-model="values[item.field.key]"
        type="password"
        autocomplete="off"
        :label="item.field.label"
        :placeholder="item.hasValue ? 'Zapisany — wpisz nowy, żeby zmienić' : 'Nie ustawiono'"
        persistent-placeholder
        :hint="item.hasValue ? 'Token jest zapisany zaszyfrowany na tym komputerze. Nie jest pokazywany.' : 'Zostanie zapisany zaszyfrowany, poza repozytorium.'"
        persistent-hint
        :append-inner-icon="item.hasValue ? 'mdi-lock-check-outline' : 'mdi-lock-open-outline'"
      >
        <template v-if="item.hasValue" #append>
          <v-btn variant="text" size="small" title="Usuń zapisany token" icon="mdi-delete-outline" @click="clearSecret(item.field.key)" />
        </template>
      </v-text-field>
      <v-text-field
        v-else
        v-model="values[item.field.key]"
        :type="item.field.kind === 'Number' ? 'number' : 'text'"
        :min="item.field.kind === 'Number' ? 0 : undefined"
        :label="item.field.label"
        :hint="item.field.hint ?? undefined"
        :persistent-hint="!!item.field.hint"
        :prepend-inner-icon="item.field.kind === 'Folder' ? 'mdi-folder-outline' : undefined"
      />
    </template>

    <v-alert v-if="test" :type="test.ok ? 'success' : 'error'" variant="tonal" density="compact" :title="test.ok ? 'Połączenie działa' : 'Połączenie nie działa'">
      {{ test.message }}
    </v-alert>

    <div class="actions">
      <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save-outline" :disabled="!dirty" :loading="saving" @click="save">
        Zapisz
      </v-btn>
      <v-btn v-if="module.canTest" variant="tonal" prepend-icon="mdi-connection" :loading="testing" @click="runTest">
        Sprawdź połączenie
      </v-btn>
    </div>
  </v-card>
</template>

<style scoped>
.module {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1.5rem;
}

.head {
  display: flex;
  gap: 0.9rem;
  align-items: flex-start;
}

.icon {
  display: grid;
  place-items: center;
  flex: none;
  width: 40px;
  height: 40px;
  border-radius: 11px;
  background: rgba(139, 92, 246, 0.18);
}

h2 {
  margin: 0;
  font-size: 1.15rem;
}

p {
  margin: 0.2rem 0 0;
  font-size: 0.875rem;
}

.actions {
  display: flex;
  gap: 0.75rem;
  margin-top: auto;
}
</style>
