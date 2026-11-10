import { config } from '@vue/test-utils'

import { vuetify } from '@/plugins/vuetify'

// jsdom nie ma ResizeObserver, a Vuetify go używa.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}

globalThis.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver

// Każdy montowany w testach komponent dostaje Vuetify.
config.global.plugins = [vuetify]
