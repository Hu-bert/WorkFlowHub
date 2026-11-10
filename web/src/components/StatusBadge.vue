<script setup lang="ts">
import Tag from 'primevue/tag'

import type { StatusPresentation } from '@/shared/status'

defineProps<StatusPresentation>()
</script>

<template>
  <Tag :severity="severity" :icon="icon" :value="label" rounded />
</template>
