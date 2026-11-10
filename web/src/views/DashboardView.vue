<script setup lang="ts">
import Button from 'primevue/button'

import StatCard from '@/components/StatCard.vue'

// Faza 1: same wartości startowe. Prawdziwe dane od Fazy 2–3.
const stats = [
  { label: 'W toku', value: '0', icon: 'pi pi-play-circle' },
  { label: 'Czeka na Ciebie', value: '0', icon: 'pi pi-user-edit', hint: 'kroki do akceptacji' },
  { label: 'Tokeny dziś', value: '0', icon: 'pi pi-bolt' },
  { label: 'Koszt dziś', value: '—', icon: 'pi pi-wallet' },
]
</script>

<template>
  <section class="dashboard">
    <header class="header">
      <div>
        <h1>Dashboard</h1>
        <p>Twoje zadania, przepływy i koszty w jednym miejscu.</p>
      </div>
      <Button label="Nowe zadanie" icon="pi pi-plus" disabled title="Faza 3" />
    </header>

    <div class="stats">
      <StatCard v-for="stat in stats" :key="stat.label" v-bind="stat" />
    </div>

    <div class="fh-card empty">
      <i class="pi pi-inbox" />
      <h2>Brak zadań</h2>
      <p>Tworzenie zadań i flow pojawi się w kolejnych fazach.</p>
    </div>
  </section>
</template>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: 1.75rem;
  max-width: 1400px;
}

.header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

h1 {
  margin: 0;
  font-size: 2rem;
  font-weight: 700;
}

.header p {
  margin: 0.35rem 0 0;
  color: var(--fh-muted);
}

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

.empty i {
  font-size: 2.5rem;
  color: #a78bfa;
}

.empty h2 {
  margin: 0.5rem 0 0;
  font-size: 1.25rem;
}

.empty p {
  margin: 0;
  color: var(--fh-muted);
}
</style>
