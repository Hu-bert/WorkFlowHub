import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { fetchWorkItems, moveWorkItem } from '@/api/workItems'
import { workItem } from '@/test/mountApp'

import { useWorkItems } from '../workItems'

vi.mock('@/api/workItems')

describe('workItems store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(fetchWorkItems).mockResolvedValue([workItem({ id: 'a', boardColumn: 'todo' })])
  })

  it('moves the card immediately and keeps it after the API confirms', async () => {
    vi.mocked(moveWorkItem).mockResolvedValue(undefined)
    const store = useWorkItems()
    await store.load()

    await store.move('a', 'done')

    expect(store.inColumn('done')).toHaveLength(1)
    expect(moveWorkItem).toHaveBeenCalledWith('a', 'done')
  })

  it('puts the card back when the API refuses', async () => {
    vi.mocked(moveWorkItem).mockRejectedValue(new Error('Nie ma kolumny'))
    const store = useWorkItems()
    await store.load()

    await expect(store.move('a', 'nowhere')).rejects.toThrow('Nie ma kolumny')

    expect(store.inColumn('todo')).toHaveLength(1)
  })
})
