import { deleteJson, getJson, postJson, putJson } from './http'
import type { BuildStatus, CiChange, PullRequestStatus, RepositoryLinks, StatusCell } from './integrations'
import type { CodeRepository } from './types'

export interface GitCommit {
  hash: string
  author: string
  date: string
  subject: string
}

/** Lokalna kopia repo znaleziona w folderze z Ustawień. catalogName = wpis z katalogu repo albo null. */
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
  scannedAt: string | null
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
  links: RepositoryLinks
}

export interface CachedBranchStatus {
  status: BranchStatus
  checkedAt: string
}

/** Repozytorium widoczne na serwerze kodu (TFS). */
export interface RemoteRepository {
  name: string
  url: string
  defaultBranch: string
}

/** Jedno zapytanie FlowHub do serwera firmowego (bez tokenów) i jego wynik. */
export interface ApiCall {
  url: string
  result: string
  milliseconds: number
}

export interface Diagnosis {
  calls: ApiCall[]
  error: string | null
  pullRequest: string | null
  build: string | null
  release: string | null
}

export interface CiConfiguration {
  id: string
  name: string
  project: string | null
}

export interface DeployProject {
  slug: string
  name: string
}

const repo = (name: string) => `/api/repositories/${encodeURIComponent(name)}`

export const fetchLocalRepositories = (refresh = false) => getJson<LocalRepositoryScan>(`/api/repositories/local?refresh=${refresh}`)
export const addLocalRepository = (path: string) => postJson<CodeRepository>('/api/repositories/local', { path })

export const fetchBranchStatus = (name: string, branch: string, refresh = false) =>
  getJson<CachedBranchStatus>(`${repo(name)}/branch-status?branch=${encodeURIComponent(branch)}&refresh=${refresh}`)

export const diagnose = (name: string, branch: string) =>
  getJson<Diagnosis>(`${repo(name)}/diagnose?branch=${encodeURIComponent(branch)}`)

export const fetchRemoteRepositories = () => getJson<RemoteRepository[]>('/api/repositories/remote')
export const addRemoteRepository = (name: string) => postJson<CodeRepository>('/api/repositories/remote', { name })

export const createRepository = (repository: CodeRepository) => postJson<CodeRepository>('/api/repositories', repository)
export const updateRepository = (originalName: string, repository: CodeRepository) => putJson<CodeRepository>(repo(originalName), repository)
export const removeRepository = (name: string) => deleteJson<void>(repo(name))

export const fetchCiConfigurations = () => getJson<CiConfiguration[]>('/api/repositories/ci-configurations')
export const fetchDeployProjects = () => getJson<DeployProject[]>('/api/repositories/deploy-projects')

/** Zapamiętana mapa repo (wynik analizy technologicznej), używana ponownie w kolejnych zadaniach. */
export interface RepoMap {
  repository: string
  content: string
  savedAt: string
  commit: string | null
  taskKey: string | null
}

export const fetchRepoMap = (name: string) => getJson<RepoMap>(`${repo(name)}/map`)
export const saveRepoMap = (name: string, content: string) => putJson<RepoMap>(`${repo(name)}/map`, { content })
export const deleteRepoMap = (name: string) => deleteJson<void>(`${repo(name)}/map`)
