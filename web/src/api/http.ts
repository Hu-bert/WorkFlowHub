/** Cienka warstwa nad fetch — jedyne miejsce, które zna szczegóły HTTP. */
export async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url, { headers: { Accept: 'application/json' } })

  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}: ${url}`)
  }

  return (await response.json()) as T
}
