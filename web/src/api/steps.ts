import { postJson, putJson } from './http'
import type { WorkItemDetails } from './types'

export interface EditProposalRequest {
  proposal: string
  model: string | null
  tokenBudget: number | null
}

export const startFlow = (workItemId: string) => postJson<WorkItemDetails>(`/api/work-items/${workItemId}/start`)

export const editProposal = (stepId: string, request: EditProposalRequest) =>
  putJson<WorkItemDetails>(`/api/steps/${stepId}/proposal`, request)

export const approveStep = (stepId: string) => postJson<WorkItemDetails>(`/api/steps/${stepId}/approve`)
export const skipStep = (stepId: string) => postJson<WorkItemDetails>(`/api/steps/${stepId}/skip`)
export const retryStep = (stepId: string, remark: string | null) =>
  postJson<WorkItemDetails>(`/api/steps/${stepId}/retry`, { remark })

/** „Co dalej?” — polecenie z kroku ręcznego wraca do kroku agenta wskazanego w flow (followUp). */
export const followUpStep = (stepId: string, remark: string, repo: string | null) =>
  postJson<WorkItemDetails>(`/api/steps/${stepId}/follow-up`, { remark, repo })

export const cancelStep = (stepId: string) => postJson<void>(`/api/steps/${stepId}/cancel`)
