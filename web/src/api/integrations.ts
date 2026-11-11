import { getJson } from './http'

export type HealthLevel = 'Ok' | 'Unknown' | 'Pending' | 'Warning' | 'Failed'

export interface StatusCell {
  level: HealthLevel
  text: string
  url: string | null
}

export interface Reviewer {
  name: string
  vote: 'Waiting' | 'Approved' | 'ApprovedWithSuggestions' | 'Rejected'
}

export interface PullRequestStatus {
  id: number
  title: string
  state: 'Draft' | 'Active' | 'Completed' | 'Abandoned'
  reviewers: Reviewer[]
  unresolvedComments: number
  policies: { name: string; passed: boolean }[]
  hasConflicts: boolean
  url: string
}

export interface BuildStatus {
  configuration: string
  number: string
  state: 'Queued' | 'Running' | 'Success' | 'Failed'
  failedTests: string[]
  url: string
}

export interface CiChange {
  revision: string
  author: string
  comment: string
  date: string
}

export interface RepoStatus {
  repo: string
  branch: string
  localPath: string
  tests: StatusCell
  pullRequest: StatusCell
  build: StatusCell
  release: string | null
  environments: { environment: string; status: StatusCell }[]
  level: HealthLevel
  pullRequestDetails: PullRequestStatus | null
  builds: BuildStatus[]
  changes: CiChange[]
}

export interface WorkItemRepoStatus {
  workItemId: string
  checkedAt: string
  summary: string
  level: HealthLevel
  repos: RepoStatus[]
}

export interface RepoLevel {
  repo: string
  level: HealthLevel
}

export interface RepoDiff {
  repo: string
  baseBranch: string
  stat: string
  patch: string
  untrackedFiles: string[]
  truncated: boolean
}

export interface TaskDetails {
  key: string
  title: string
  description: string
  url: string
}

export const fetchRepoStatus = (workItemId: string, refresh = false) =>
  getJson<WorkItemRepoStatus>(`/api/work-items/${workItemId}/repo-status?refresh=${refresh}`)

export const fetchRepoLevels = () => getJson<Record<string, RepoLevel[]>>('/api/repo-status/levels')

export const fetchDiff = (workItemId: string, repo: string) =>
  getJson<RepoDiff>(`/api/work-items/${workItemId}/repos/${encodeURIComponent(repo)}/diff`)

export const fetchTaskFromTracker = (key: string) =>
  getJson<TaskDetails>(`/api/task-source/${encodeURIComponent(key)}`)
