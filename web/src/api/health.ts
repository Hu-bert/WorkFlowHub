import { getJson } from './http'

export interface HealthResponse {
  status: string
  version: string
}

export const fetchHealth = () => getJson<HealthResponse>('/api/health')
