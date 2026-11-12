import { describe, expect, it } from 'vitest'

import { mountApp } from '@/test/mountApp'

import RepoDetails from '../RepoDetails.vue'

const cell = (level: 'Ok' | 'Unknown' | 'Failed', text: string) => ({ level, text, url: null })

const props = (tab: string) => ({
  tab,
  pullRequest: cell('Failed', 'Konflikty scalania'),
  pullRequestDetails: null,
  build: cell('Failed', '#1.30.0 #643 · 24 testów'),
  builds: [
    {
      configuration: 'Kcf_Production',
      number: '1.30.0 #643',
      state: 'Failed' as const,
      failedTests: ['SystemMonitor.Tests.Service.ExclusionsTests.BusCheck_filters("Chalmer",True)'],
      url: 'https://tc/build/643',
      statusText: 'Tests failed: 24 (24 new), passed: 194',
      branch: 'ZR-3925-2',
    },
  ],
  changes: [],
  environments: [
    { environment: 'VDEV2', status: cell('Ok', '1.30.0') },
    { environment: 'TEST-CD', status: cell('Unknown', '1.29.1') },
    { environment: 'PROD-CD', status: cell('Unknown', '—') },
  ],
  release: '1.30.0',
  links: null,
})

describe('RepoDetails', () => {
  it('build tab shows the status text and failed tests, with the test name emphasised over its namespace', async () => {
    const wrapper = await mountApp(RepoDetails, props('build'))

    expect(wrapper.text()).toContain('nie przeszedł')
    expect(wrapper.text()).toContain('Tests failed: 24 (24 new), passed: 194')
    expect(wrapper.find('.test').attributes('title')).toContain('SystemMonitor.Tests.Service.ExclusionsTests.BusCheck_filters')
    expect(wrapper.find('.test .fh-muted').text()).toBe('SystemMonitor.Tests.Service.')
    expect(wrapper.text()).not.toContain('Release z tego brancha')
  })

  it('deployments tab compares the version on every environment with the one from the branch', async () => {
    const wrapper = await mountApp(RepoDetails, props('deploy'))
    const rows = wrapper.findAll('.deployments tr').map((row) => row.text())

    expect(wrapper.text()).toContain('Release z tego brancha: 1.30.0')
    expect(rows[0]).toContain('VDEV2')
    expect(rows[0]).toContain('1.30.0')
    expect(rows[1]).toContain('inna wersja niż z tego brancha')
    expect(rows[2]).toContain('nic nie wdrożono')
  })

  it('pull request tab says plainly when there is no pull request', async () => {
    const wrapper = await mountApp(RepoDetails, props('pr'))

    expect(wrapper.text()).toContain('Brak pull requesta dla tego brancha')
  })
})