import { getJson, postJson } from './http'
import type { BoardColumn, CodeRepository, FlowsResponse, RepositorySuggestion } from './types'

export const fetchFlows = () => getJson<FlowsResponse>('/api/flows')
export const reloadFlows = () => postJson<FlowsResponse>('/api/flows/reload')

export const fetchRepositories = () => getJson<CodeRepository[]>('/api/repositories')
export const reloadRepositories = () => postJson<CodeRepository[]>('/api/repositories/reload')
export const suggestRepositories = (text: string) =>
  postJson<RepositorySuggestion[]>('/api/repositories/suggest', { text })

export const fetchBoard = () => getJson<BoardColumn[]>('/api/board')
