import { beforeEach, describe, expect, it, vi } from 'vitest'

import { fetchRepoStatus, type RepoStatus, type StatusCell, type WorkItemRepoStatus } from '@/api/integrations'
import { mountApp } from '@/test/mountApp'

import RepoStatusTable from '../RepoStatusTable.vue'

vi.mock('@/api/integrations')
vi.mock('@/api/live', () => ({ liveConnection: () => ({ on: vi.fn(), off: vi.fn() }) }))

const cell = (level: StatusCell['level'], text: string, url: string | null = null): StatusCell => ({ level, text, url })

const repo = (name: string, overrides: Partial<RepoStatus> = {}): RepoStatus => ({
  repo: name,
  branch: 'feature/PROJ-1',
  localPath: `C:\\work\\PROJ-1\\${name}`,
  tests: cell('Ok', 'Przeszły'),
  pullRequest: cell('Pending', '2/3 akcept.', 'https://tfs/pr/1'),
  build: cell('Ok', '#4512', 'https://tc/4512'),
  release: '1.8.3',
  environments: [
    { environment: 'DEV', status: cell('Ok', '1.8.3') },
    { environment: 'TEST', status: cell('Pending', 'Wdrażanie') },
  ],
  level: 'Pending',
  pullRequestDetails: null,
  builds: [],
  changes: [],
  links: { repository: null, pullRequests: null, builds: [], deploy: null },
  ...overrides,
})

const status = (repos: RepoStatus[]): WorkItemRepoStatus => ({
  workItemId: 'item-1',
  checkedAt: '2026-11-11T08:00:00Z',
  summary: '1 z 2 repo gotowe na DEV',
  level: 'Failed',
  repos,
})

describe('RepoStatusTable', () => {
  beforeEach(() => vi.clearAllMocks())

  it('shows one row per repo with a column for every environment', async () => {
    vi.mocked(fetchRepoStatus).mockResolvedValue(status([repo('Billing.Api'), repo('Shared.Lib')]))

    const wrapper = await mountApp(RepoStatusTable, { workItemId: 'item-1', refreshKey: 0 })

    expect(wrapper.findAll('tr.repo-row')).toHaveLength(2)
    expect(wrapper.findAll('th').map((th) => th.text())).toEqual(
      ['Repo', 'Branch', 'Testy', 'PR (TFS)', 'Build (TC)', 'Release', 'DEV', 'TEST', 'Akcje'],
    )
    expect(wrapper.text()).toContain('1 z 2 repo gotowe na DEV')
  })

  it('every status cell has text and an icon, and links to the source system', async () => {
    vi.mocked(fetchRepoStatus).mockResolvedValue(status([repo('Billing.Api')]))

    const wrapper = await mountApp(RepoStatusTable, { workItemId: 'item-1', refreshKey: 0 })
    const prCell = wrapper.findAll('.health-cell').find((candidate) => candidate.text() === '2/3 akcept.')!

    expect(prCell.attributes('href')).toBe('https://tfs/pr/1')
    expect(prCell.find('.v-icon').exists()).toBe(true)
    expect(wrapper.find('a[href^="vscode://file/"]').attributes('href')).toBe('vscode://file/C:/work/PROJ-1/Billing.Api')
  })

  it('"Odśwież teraz" asks the servers again instead of using the cache', async () => {
    vi.mocked(fetchRepoStatus).mockResolvedValue(status([repo('Billing.Api')]))
    const wrapper = await mountApp(RepoStatusTable, { workItemId: 'item-1', refreshKey: 0 })

    await wrapper.findAll('button').find((button) => button.text().includes('Odśwież teraz'))!.trigger('click')

    expect(fetchRepoStatus).toHaveBeenNthCalledWith(1, 'item-1', false)
    expect(fetchRepoStatus).toHaveBeenNthCalledWith(2, 'item-1', true)
  })

  it('clicking a row shows reviewers, policies and failed tests', async () => {
    vi.mocked(fetchRepoStatus).mockResolvedValue(
      status([
        repo('Customer.Portal', {
          pullRequestDetails: {
            id: 7,
            title: 'PR',
            state: 'Active',
            reviewers: [{ name: 'Anna Nowak', vote: 'Rejected' }],
            unresolvedComments: 3,
            policies: [{ name: 'Build validation', passed: false }],
            hasConflicts: true,
            url: 'u',
          },
          builds: [{ configuration: 'Portal_Build', number: '12', state: 'Failed', failedTests: ['Checkout.VatRate'], url: 'u', statusText: null, branch: null }],
        }),
      ]),
    )
    const wrapper = await mountApp(RepoStatusTable, { workItemId: 'item-1', refreshKey: 0 })

    await wrapper.find('tr.repo-row').trigger('click')

    expect(wrapper.text()).toContain('Anna Nowak: odrzucił')
    expect(wrapper.text()).toContain('Build validation: niespełniona')
    expect(wrapper.text()).toContain('Konflikty scalania')
    expect(wrapper.text()).toContain('Checkout.VatRate')
  })
})
