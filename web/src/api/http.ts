/** Błąd z API z komunikatem gotowym do pokazania użytkownikowi. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message)
  }
}

/** Cienka warstwa nad fetch — jedyne miejsce, które zna szczegóły HTTP. */
async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    headers: { Accept: 'application/json', ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (!response.ok) {
    throw new ApiError(await errorMessage(response), response.status)
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** Backend zwraca ProblemDetails — pole `detail` jest napisane dla człowieka. */
async function errorMessage(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    return problem.detail ?? problem.title ?? `${response.status} ${response.statusText}`
  } catch {
    return `${response.status} ${response.statusText}`
  }
}

export const getJson = <T>(url: string) => request<T>('GET', url)
export const postJson = <T>(url: string, body?: unknown) => request<T>('POST', url, body ?? {})
export const putJson = <T>(url: string, body: unknown) => request<T>('PUT', url, body)
export const deleteJson = <T>(url: string) => request<T>('DELETE', url)
