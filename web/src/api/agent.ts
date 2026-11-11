import { getJson, postJson } from './http'
import type { FileContent, PermissionRequest, WorkItemFile } from './types'

export const fetchPermissions = (workItemId: string) =>
  getJson<PermissionRequest[]>(`/api/work-items/${workItemId}/permissions`)

export const decidePermission = (requestId: string, allow: boolean) =>
  postJson<void>(`/api/permissions/${requestId}`, { allow })

export const fetchFiles = (workItemId: string) => getJson<WorkItemFile[]>(`/api/work-items/${workItemId}/files`)

export const fetchFile = (workItemId: string, name: string) =>
  getJson<FileContent>(`/api/work-items/${workItemId}/files/${encodeURIComponent(name)}`)
