// Typy DTO z backendu — jedno miejsce. Nazwy pól = JSON z API (camelCase).

export type StepKind = 'Script' | 'Agent' | 'Manual' | 'Check'
export type StepStatus = 'Pending' | 'Proposed' | 'Approved' | 'Running' | 'Succeeded' | 'Failed' | 'Skipped'
export type TaskSourceKind = 'Manual' | 'Tracker'
export type RepoRole = 'Main' | 'Dependent'

export interface TokenUsage {
  model: string | null
  inputTokens: number
  outputTokens: number
  cacheReadTokens: number
  cacheWriteTokens: number
  totalTokens: number
  cost: number
}

export interface BoardColumn {
  key: string
  label: string
  icon: string
}

export interface CodeRepository {
  name: string
  url: string
  defaultBranch: string
  ciBuildConfigurations: string[]
  deployProject: string | null
  keywords: string[]
}

export interface RepositorySuggestion {
  name: string
  matchedKeywords: string[]
}

export interface StepDefinition {
  id: string
  name: string
  kind: StepKind
  script: string | null
  agent: string | null
  prompt: string | null
  check: string | null
  checklist: string[]
  /** Manual: id kroku agenta, do którego można stąd wysłać polecenie „co dalej”. */
  followUp: string | null
  model: string | null
  tokenBudget: number
  requiresApproval: boolean
  perRepo: boolean
  allowedTools: string[]
  inputs: string[]
  outputs: string[]
}

export interface FlowDefinition {
  type: string
  name: string
  version: number
  steps: StepDefinition[]
}

export interface FlowProblem {
  fileName: string
  errors: string[]
}

export interface FlowsResponse {
  flows: FlowDefinition[]
  problems: FlowProblem[]
}

export interface TaskRepo {
  name: string
  branch: string
  localPath: string
  role: RepoRole
}

export interface StepRun {
  id: string
  stepId: string
  name: string
  kind: StepKind
  order: number
  repoName: string | null
  status: StepStatus
  requiresApproval: boolean
  proposal: string | null
  model: string | null
  tokenBudget: number
  result: string | null
  error: string | null
  remark: string | null
  log: string
  attempt: number
  startedAt: string | null
  finishedAt: string | null
  usage: TokenUsage
  /** Id sesji Copilot CLI — jest tylko po kroku wykonanym prawdziwym CLI. */
  sessionId: string | null
}

export interface WorkItemSummary {
  id: string
  key: string
  title: string
  type: string
  boardColumn: string
  createdAt: string
  currentStepName: string | null
  currentStepStatus: StepStatus | null
  waitsForUser: boolean
  stepsDone: number
  stepsTotal: number
  repositories: string[]
  usage: TokenUsage
}

export interface WorkItemDetails {
  summary: WorkItemSummary
  description: string
  source: TaskSourceKind
  sourceUrl: string | null
  workDirectory: string
  repos: TaskRepo[]
  steps: StepRun[]
}

/** Prośba agenta o zgodę na ryzykowne narzędzie (write, shell). */
export interface PermissionRequest {
  id: string
  workItemId: string
  stepRunId: string
  tool: string
  detail: string
}

export interface WorkItemFile {
  name: string
  size: number
  modifiedAt: string
}

export interface FileContent {
  name: string
  content: string
}

export interface CreateWorkItemRequest {
  key: string
  title: string
  description: string
  type: string
  source: TaskSourceKind
  sourceUrl: string | null
  repositories: string[]
}
