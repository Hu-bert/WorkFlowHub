<script setup lang="ts">
import StatCard from '@/components/StatCard.vue'

// Faza 1: same wartości startowe. Prawdziwe dane od Fazy 2–3.
const stats = [
  { label: 'W toku', value: '0', icon: 'mdi-play-circle-outline' },
  { label: 'Czeka na Ciebie', value: '0', icon: 'mdi-account-clock-outline', hint: 'kroki do akceptacji' },
  { label: 'Tokeny dziś', value: '0', icon: 'mdi-lightning-bolt-outline' },
  { label: 'Koszt dziś', value: '—', icon: 'mdi-wallet-outline' },
]
</script>

<template>
  <section class="fh-page">
    <header class="fh-page-header">
      <div>
        <h1>Dashboard</h1>
        <p>Twoje zadania, przepływy i koszty w jednym miejscu.</p>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" disabled>Nowe zadanie</v-btn>
    </header>

    <div class="stats">
      <StatCard v-for="stat in stats" :key="stat.label" v-bind="stat" />
    </div>

    <v-card class="empty">
      <v-icon icon="mdi-inbox-outline" size="48" color="primary" />
      <h2>Brak zadań</h2>
      <p class="fh-muted">Tworzenie zadań i flow pojawi się w kolejnych fazach.</p>
    </v-card>
  </section>
</template>

<style scoped>
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.4rem;
  padding: 3.5rem 1rem;
  text-align: center;
}

.empty h2 {
  margin: 0.5rem 0 0;
  font-size: 1.25rem;
}

.empty p {
  margin: 0;
}
</style>
