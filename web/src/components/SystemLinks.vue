<script setup lang="ts">
import type { RepositoryLinks } from '@/api/integrations'

/** Skróty „przejdź do…” — repo w TFS, build configuration w TeamCity, projekt w Octopus. Pokazujemy tylko znane adresy. */
defineProps<{ links: RepositoryLinks | null | undefined }>()
</script>

<template>
  <span v-if="links" class="links" @click.stop>
    <v-btn v-if="links.repository" :href="links.repository" target="_blank" rel="noopener" size="x-small" variant="tonal" prepend-icon="mdi-source-branch" title="Repo i branch w TFS">
      TFS
    </v-btn>
    <v-btn v-if="links.pullRequests" :href="links.pullRequests" target="_blank" rel="noopener" size="x-small" variant="tonal" prepend-icon="mdi-source-pull" title="Pull requesty repo w TFS">
      PR
    </v-btn>
    <v-btn
      v-for="build in links.builds"
      :key="build.url"
      :href="build.url"
      target="_blank"
      rel="noopener"
      size="x-small"
      variant="tonal"
      prepend-icon="mdi-hammer-wrench"
      :title="`TeamCity: ${build.name}`"
    >
      TC
    </v-btn>
    <v-btn v-if="links.deploy" :href="links.deploy" target="_blank" rel="noopener" size="x-small" variant="tonal" prepend-icon="mdi-rocket-launch-outline" title="Projekt w Octopus Deploy">
      Octopus
    </v-btn>
  </span>
</template>

<style scoped>
.links {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}
</style>
