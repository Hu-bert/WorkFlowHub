import { getJson } from './http'

export interface TokenReportRow {
  label: string
  inputTokens: number
  outputTokens: number
  cacheReadTokens: number
  totalTokens: number
  cost: number
  runs: number
}

export interface TokenReport {
  total: TokenReportRow
  byModel: TokenReportRow[]
  byType: TokenReportRow[]
  byWorkItem: TokenReportRow[]
  byStep: TokenReportRow[]
  byDay: TokenReportRow[]
}

export interface ModelsInfo {
  defaultTier: string
  tiers: { tier: string; model: string }[]
  taskTokenBudget: number
  agentRunner: string
  /** Plan bez limitu (np. Copilot Enterprise): bez budżetów i wyceny. */
  unlimited: boolean
}

export const fetchTokenReport = () => getJson<TokenReport>('/api/tokens/report')
export const fetchModels = () => getJson<ModelsInfo>('/api/models')
