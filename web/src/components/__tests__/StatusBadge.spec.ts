import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import { describe, expect, it } from 'vitest'

import { connectionPresentation } from '@/shared/connectionState'

import StatusBadge from '../StatusBadge.vue'

describe('StatusBadge', () => {
  it.each(Object.values(connectionPresentation))('shows text and icon: $label', (status) => {
    const wrapper = mount(StatusBadge, { props: status, global: { plugins: [PrimeVue] } })

    expect(wrapper.text()).toContain(status.label)
    expect(wrapper.find('.pi').classes()).toContain(status.icon.split(' ').at(-1))
  })
})
