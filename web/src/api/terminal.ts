import { getJson } from './http'

/** Co otworzyć w terminalu: repo zadania ('' = katalog zadania) albo sesję kroku do kontynuacji. */
export interface TerminalTarget {
  workItemId: string
  repo?: string
  stepId?: string
}

function query(target: TerminalTarget, extra: Record<string, string> = {}) {
  const params = new URLSearchParams(extra)
  if (target.repo !== undefined) params.set('repo', target.repo)
  if (target.stepId) params.set('step', target.stepId)
  return params.toString()
}

/** Sprawdza, czy terminal da się uruchomić (katalog istnieje, krok ma sesję). Rzuca czytelny błąd. */
export const checkTerminal = (target: TerminalTarget) =>
  getJson<{ workingDirectory: string; arguments: string[] }>(`/api/work-items/${target.workItemId}/terminal?${query(target)}`)

export function terminalUrl(target: TerminalTarget, cols: number, rows: number) {
  const scheme = location.protocol === 'https:' ? 'wss' : 'ws'
  const params = query(target, { workItemId: target.workItemId, cols: String(cols), rows: String(rows) })
  return `${scheme}://${location.host}/ws/terminal?${params}`
}
