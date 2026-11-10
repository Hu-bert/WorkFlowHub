import { getJson, postJson, putJson } from './http'
import type { CreateWorkItemRequest, WorkItemDetails, WorkItemSummary } from './types'

export const fetchWorkItems = () => getJson<WorkItemSummary[]>('/api/work-items')
export const fetchWorkItem = (id: string) => getJson<WorkItemDetails>(`/api/work-items/${id}`)
export const createWorkItem = (request: CreateWorkItemRequest) =>
  postJson<WorkItemDetails>('/api/work-items', request)
export const moveWorkItem = (id: string, column: string) =>
  putJson<void>(`/api/work-items/${id}/column`, { column })
