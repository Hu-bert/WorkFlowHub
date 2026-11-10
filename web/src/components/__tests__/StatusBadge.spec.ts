import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import { connectionPresentation } from '@/shared/connectionState'

import StatusBadge from '../StatusBadge.vue'

describe('StatusBadge', () => {
  it.each(Object.values(connectionPresentation))('shows text and icon: $label', (status) => {
    const wrapper = mount(StatusBadge, { props: status })

    expect(wrapper.text()).toContain(status.label)
    expect(wrapper.find('.v-icon').classes()).toContain(status.icon.split(' ')[0])
  })
})
