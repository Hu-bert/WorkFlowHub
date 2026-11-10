<script setup lang="ts">
import AppSidebar from './AppSidebar.vue'
</script>

<template>
  <v-app>
    <AppSidebar />
    <v-main>
      <div class="content">
        <slot />
      </div>
    </v-main>
  </v-app>
</template>

<style scoped>
.content {
  padding: 2rem 2.5rem;
}

@media (max-width: 800px) {
  .content {
    padding: 1.25rem 1rem;
  }
}
</style>
