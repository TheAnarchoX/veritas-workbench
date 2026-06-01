const rawBase = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080/api'
export const API_BASE = rawBase.replace(/\/$/, '')

export function apiUrl(path: string) {
  const normalized = path.startsWith('/') ? path : `/${path}`
  return `${API_BASE}${normalized}`
}

export async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(apiUrl(path))
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`)
  }
  return response.json() as Promise<T>
}

export async function postJson<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(apiUrl(path), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || `${response.status} ${response.statusText}`)
  }
  return response.json() as Promise<T>
}

export async function patchJson<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(apiUrl(path), {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || `${response.status} ${response.statusText}`)
  }
  return response.json() as Promise<T>
}

export async function postForm<T>(path: string, form: FormData): Promise<T> {
  const response = await fetch(apiUrl(path), {
    method: 'POST',
    body: form,
  })
  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || `${response.status} ${response.statusText}`)
  }
  return response.json() as Promise<T>
}

export async function postEmpty<T>(path: string): Promise<T> {
  const response = await fetch(apiUrl(path), { method: 'POST' })
  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || `${response.status} ${response.statusText}`)
  }
  return response.json() as Promise<T>
}

export async function getText(path: string): Promise<string> {
  const response = await fetch(apiUrl(path))
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`)
  }
  return response.text()
}
