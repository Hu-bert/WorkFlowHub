import { getJson, putJson } from './http'
import type { StepKind } from './types'

/** Krok w edytorze — dokładnie to, co trafia do pliku YAML. */
export interface StepDocument {
  id: string
  name: string
  kind: StepKind
  script: string | null
  agent: string | null
  prompt: string | null
  check: string | null
  checklist: string[]
  model: string | null
  tokenBudget: number
  requiresApproval: boolean
  perRepo: boolean
  allowedTools: string[]
  inputs: string[]
  outputs: string[]
}

export interface FlowDocument {
  type: string
  name: string
  version: number
  flow: StepDocument[]
}

export interface SaveFlowResponse {
  saved: boolean
  errors: string[]
}

export interface BlockFile {
  kind: 'agents' | 'skills' | 'prompts' | 'scripts' | 'templates'
  path: string
  name: string
}

export interface BlockContent {
  path: string
  content: string
}

export const fetchFlowDocument = (type: string) => getJson<FlowDocument>(`/api/flows/${encodeURIComponent(type)}/document`)
export const saveFlow = (document: FlowDocument) => putJson<SaveFlowResponse>('/api/flows', document)

export const fetchBlocks = () => getJson<BlockFile[]>('/api/blocks')
export const fetchBlockContent = (path: string) => getJson<BlockContent>(`/api/blocks/content?path=${encodeURIComponent(path)}`)
export const saveBlockContent = (block: BlockContent) => putJson<void>('/api/blocks/content', block)

export const emptyStep = (kind: StepKind): StepDocument => ({
  id: '',
  name: '',
  kind,
  script: null,
  agent: null,
  prompt: null,
  check: null,
  checklist: [],
  model: kind === 'Agent' ? 'tier:cheap' : null,
  tokenBudget: kind === 'Agent' ? 30000 : 0,
  requiresApproval: true,
  perRepo: false,
  allowedTools: kind === 'Agent' ? ['read'] : [],
  inputs: [],
  outputs: [],
})
