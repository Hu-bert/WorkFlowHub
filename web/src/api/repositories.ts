import { getJson, postJson } from './http'
import type { BuildStatus, CiChange, PullRequestStatus, StatusCell } from './integrations'
import type { CodeRepository } from './types'

export interface GitCommit {
  hash: string
  author: string
  date: string
  subject: string
}

/** Lokalna kopia repo znaleziona w folderze z Ustawień. catalogName = wpis z repos.yaml albo null. */
export interface LocalRepository {
  path: string
  folder: string
  name: string
  remoteUrl: string | null
  branch: string
  recentCommits: GitCommit[]
  uncommittedFiles: number
  catalogName: string | null
}

export interface LocalRepositoryScan {
  /** null = folder nie jest ustawiony. */
  root: string | null
  repositories: LocalRepository[]
}

/** Stan brancha w TFS / TeamCity / Octopus. */
export interface BranchStatus {
  pullRequest: StatusCell
  build: StatusCell
  release: string | null
  environments: { environment: string; status: StatusCell }[]
  level: StatusCell['level']
  pullRequestDetails: PullRequestStatus | null
  builds: BuildStatus[]
  changes: CiChange[]
}

/** Repozytorium widoczne na serwerze kodu (TFS). */
export interface RemoteRepository {
  name: string
  url: string
  defaultBranch: string
  webUrl: string | null
}

export const fetchLocalRepositories = () => getJson<LocalRepositoryScan>('/api/repositories/local')
export const addLocalRepository = (path: string) => postJson<CodeRepository>('/api/repositories/local', { path })

export const fetchBranchStatus = (name: string, branch: string) =>
  getJson<BranchStatus>(`/api/repositories/${encodeURIComponent(name)}/branch-status?branch=${encodeURIComponent(branch)}`)

export const fetchRemoteRepositories = () => getJson<RemoteRepository[]>('/api/repositories/remote')
export const addRemoteRepository = (name: string) => postJson<CodeRepository>('/api/repositories/remote', { name })
