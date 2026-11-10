const numberFormat = new Intl.NumberFormat('pl-PL')
const costFormat = new Intl.NumberFormat('pl-PL', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 4 })
const dateTimeFormat = new Intl.DateTimeFormat('pl-PL', { dateStyle: 'short', timeStyle: 'short' })

/** 12345 → "12,3 tys." — krótko, do kart i kafelków. */
export function formatTokens(tokens: number): string {
  if (tokens >= 1_000_000) return `${(tokens / 1_000_000).toFixed(1).replace('.', ',')} mln`
  if (tokens >= 10_000) return `${(tokens / 1000).toFixed(1).replace('.', ',')} tys.`
  return numberFormat.format(tokens)
}

export const formatCost = (cost: number) => (cost === 0 ? '—' : costFormat.format(cost))

export const formatDateTime = (iso: string | null) => (iso ? dateTimeFormat.format(new Date(iso)) : '—')

export function formatDuration(startIso: string | null, endIso: string | null): string {
  if (!startIso || !endIso) return '—'

  const seconds = Math.max(0, Math.round((new Date(endIso).getTime() - new Date(startIso).getTime()) / 1000))
  return seconds < 60 ? `${seconds} s` : `${Math.floor(seconds / 60)} min ${seconds % 60} s`
}
