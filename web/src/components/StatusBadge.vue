<script setup lang="ts">
import type { StatusPresentation } from '@/shared/status'

defineProps<StatusPresentation & { size?: 'x-small' | 'small' | 'default' | 'large' }>()
</script>

<template>
  <v-chip :color="color" :prepend-icon="icon" :size="size ?? 'default'" variant="tonal" class="font-weight-medium">
    {{ label }}
  </v-chip>
</template>
