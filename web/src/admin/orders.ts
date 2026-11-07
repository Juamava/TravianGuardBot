import type { RealtimeChannel } from '@supabase/supabase-js'

export type OnlineClient = { username: string; host: string; clan: string }
export type OrderAction = 'stop' | 'resume' | 'update'
export type OrderFeature = 'all' | 'farming' | 'building'
export type Order = {
  id: string
  username: string
  host: string
  action: OrderAction
  feature: OrderFeature
  reason: string
  by: string
}

type PresenceListener = (people: OnlineClient[]) => void
const listeners = new Set<PresenceListener>()
let people: OnlineClient[] = []
let channel: RealtimeChannel | null = null

export function watchClients(listener: PresenceListener) {
  listeners.add(listener)
  listener(people)
  return () => { listeners.delete(listener) }
}

function publish(next: OnlineClient[]) {
  people = next
  listeners.forEach(listener => listener(people))
}

export function attachClients(next: RealtimeChannel) {
  channel = next
}

export function detachClients(next: RealtimeChannel) {
  if (channel === next) {
    channel = null
    publish([])
  }
}

export function readPresence(source: RealtimeChannel) {
  const rows: OnlineClient[] = []
  for (const entries of Object.values(source.presenceState<OnlineClient>())) {
    for (const entry of entries) {
      if (!entry.username || !entry.host) continue
      rows.push({ username: entry.username, host: entry.host, clan: entry.clan || '' })
    }
  }
  const seen = new Set<string>()
  return rows.filter(row => {
    const key = `${row.host}|${row.username}`.toLowerCase()
    if (seen.has(key)) return false
    seen.add(key)
    return true
  }).sort((left, right) => left.username.localeCompare(right.username, 'es') || left.clan.localeCompare(right.clan, 'es'))
}

export function notePresence(next: OnlineClient[]) {
  publish(next)
}

export async function sendOrder(order: Order) {
  if (!channel) throw new Error('El chat no está conectado.')
  const result = await channel.send({ type: 'broadcast', event: 'order', payload: order })
  if (result !== 'ok') throw new Error('No se pudo enviar la orden.')
}
