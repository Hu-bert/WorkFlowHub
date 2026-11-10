<script setup lang="ts">
import AppSidebar from './AppSidebar.vue'
</script>

<template>
  <div class="layout">
    <AppSidebar />
    <main class="content">
      <slot />
    </main>
  </div>
</template>

<style scoped>
.layout {
  display: grid;
  grid-template-columns: 248px 1fr;
  min-height: 100vh;
}

.content {
  padding: 2rem 2.5rem;
  min-width: 0;
}

@media (max-width: 800px) {
  .layout {
    grid-template-columns: 1fr;
  }

  .content {
    padding: 1.25rem 1rem;
  }
}
</style>
