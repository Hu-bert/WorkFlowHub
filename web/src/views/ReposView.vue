<script setup lang="ts">
import { onMounted, ref } from 'vue'

import LocalRepositories from '@/components/LocalRepositories.vue'
import { useCatalog } from '@/stores/catalog'
import { useNotifications } from '@/stores/notifications'

const catalog = useCatalog()
const notifications = useNotifications()
const reloading = ref(false)

onMounted(() => catalog.load().catch(notifications.error))

async function reload() {
  reloading.value = true
  try {
    await catalog.reloadFromDisk()
    notifications.success('Wczytano repos.yaml i flows/ od nowa')
  } catch (error) {
    notifications.error(error)
  } finally {
    reloading.value = false
  }
}
</script>

<template>
  <section class="fh-page">
    <header class="fh-page-header">
      <div>
        <h1>Repozytoria</h1>
        <p>Twoje lokalne kopie oraz katalog repo z pliku <code>repos.yaml</code> (po ręcznej edycji pliku kliknij „Wczytaj ponownie”).</p>
      </div>
      <v-btn variant="tonal" color="primary" prepend-icon="mdi-refresh" :loading="reloading" @click="reload">
        Wczytaj ponownie
      </v-btn>
    </header>

    <LocalRepositories />

    <v-card title="Katalog repo (repos.yaml)" prepend-icon="mdi-source-repository">
      <v-table>
        <thead>
          <tr>
            <th>Repo</th>
            <th>Adres</th>
            <th>Branch</th>
            <th>TeamCity</th>
            <th>Octopus</th>
            <th>Słowa kluczowe</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="repository in catalog.repositories" :key="repository.name">
            <td class="font-weight-bold">{{ repository.name }}</td>
            <td class="fh-muted">{{ repository.url }}</td>
            <td>{{ repository.defaultBranch }}</td>
            <td>{{ repository.ciBuildConfigurations.join(', ') || '—' }}</td>
            <td>{{ repository.deployProject ?? '—' }}</td>
            <td>
              <v-chip v-for="keyword in repository.keywords" :key="keyword" size="x-small" class="mr-1">
                {{ keyword }}
              </v-chip>
            </td>
          </tr>
          <tr v-if="catalog.repositories.length === 0">
            <td colspan="6" class="text-center fh-muted py-6">Brak repo. Dodaj wpisy do repos.yaml.</td>
          </tr>
        </tbody>
      </v-table>
    </v-card>
  </section>
</template>
