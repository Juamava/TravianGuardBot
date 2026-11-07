export type ApiResult<T> = { ok: boolean; message: string; data: T }

export class ApiError extends Error {}

export async function api<T>(path: string, init?: RequestInit): Promise<ApiResult<T>> {
  const headers = new Headers(init?.headers)
  if (init?.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  const response = await fetch(path, { ...init, headers })
  const body = (await response.json().catch(() => ({ ok: false, message: 'Respuesta vacía', data: null }))) as ApiResult<T>
  if (!response.ok || body.ok === false) throw new ApiError(body.message || 'No se pudo completar')
  return body
}

export function post<T>(path: string, data?: unknown) {
  return api<T>(path, { method: 'POST', body: data === undefined ? undefined : JSON.stringify(data) })
}

export function put<T>(path: string, data?: unknown) {
  return api<T>(path, { method: 'PUT', body: data === undefined ? undefined : JSON.stringify(data) })
}

export function del<T>(path: string) {
  return api<T>(path, { method: 'DELETE' })
}

export type AccountRow = {
  id: number
  username: string
  server: string
  host: string
  status: string
  pause: string
  gold?: boolean
  villages?: boolean
  build?: boolean
  troops?: boolean
  trade?: boolean
  farms?: boolean
  gameFarms?: boolean
  scouts?: boolean
  adventures?: boolean
  villageNext?: string | null
  buildNext?: string | null
  troopNext?: string | null
  tradeNext?: string | null
  farmNext?: string | null
  gameFarmNext?: string | null
  scoutNext?: string | null
  adventureNext?: string | null
  clan?: string | null
  accesses?: string[]
}

export type Overview = {
  version: string
  templates: { account: string; village: string }
  accounts: AccountRow[]
}

export type Meta = {
  tribes: { name: string; value: number }[]
  troops: { name: string; value: number; tribe: number; group: string }[]
  resourcePlans: { name: string; value: string }[]
}

export type Access = {
  id: number
  username: string
  password: string
  proxyHost: string
  proxyPort: number
  proxyUsername: string
  proxyPassword: string
  useragent: string
}

export type AccountDetail = {
  id: number
  username: string
  server: string
  accesses: Access[]
}

export function emptyAccess(): Access {
  return { id: 0, username: '', password: '', proxyHost: '', proxyPort: 0, proxyUsername: '', proxyPassword: '', useragent: '' }
}

export type HeroStatus = {
  state: string
  message: string
  level: number | null
  hpPercent: number | null
  experience: number | null
  updatedAt: string | null
  statsAt: string | null
  heroLists: string[]
  minHpPercent: number
  minHealthLeft: number
}
