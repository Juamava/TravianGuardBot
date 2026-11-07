import { createClient } from '@supabase/supabase-js'

export const clanChat = createClient('https://uyzkcejpsjpavghbchoq.supabase.co', 'sb_publishable_Wdj9c0igv_Th0TNGqgN2Uw_TaA-ruCN')

export function roomOf(host: string, clan: string) {
  return `${host.trim().toLowerCase()}|${clan.trim()}`
}

export type ClanFeature = 'all' | 'farming' | 'building'
export type ClanCommand =
  | { kind: 'help' }
  | { kind: 'need-reason' }
  | { kind: 'update-usage' }
  | { kind: 'update'; target: string }
  | { kind: 'action'; action: 'stop' | 'resume'; feature: ClanFeature; target: string; reason: string }

export function parseClanCommand(body: string): ClanCommand | null {
  const parts = body.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return null
  const command = parts[0].toLowerCase()
  if (command === '/help') return parts.length === 1 ? { kind: 'help' } : null
  if (command === '/update') return parts.length === 2 ? { kind: 'update', target: parts[1] } : { kind: 'update-usage' }
  if (command !== '/stop' && command !== '/resume') return null
  if (parts.length === 1) return { kind: 'help' }
  let index = 1
  let feature: ClanFeature = 'all'
  const word = parts[1].toLowerCase()
  if (word === 'farming' || word === 'building' || word === 'aldeas') {
    feature = word === 'farming' ? 'farming' : 'building'
    index = 2
  }
  if (parts.length < index + 2) return { kind: 'need-reason' }
  return { kind: 'action', action: command === '/resume' ? 'resume' : 'stop', feature, target: parts[index], reason: parts.slice(index + 1).join(' ') }
}

export function describeClanCommand(command: ClanCommand) {
  if (command.kind === 'help' || command.kind === 'update-usage') return 'Pidió la ayuda de los comandos.'
  if (command.kind === 'need-reason') return 'Faltó la razón, así que no hizo nada.'
  if (command.kind === 'update') return command.target.localeCompare('all', 'es', { sensitivity: 'accent' }) === 0 ? 'Actualizar todos los clientes.' : `Actualizar el cliente de ${command.target}.`
  const name = command.feature === 'farming' ? 'el farming' : command.feature === 'building' ? 'la construcción' : 'el bot'
  const verb = command.action === 'resume' ? 'Reanudar' : 'Detener'
  return `${verb} ${name} de ${command.target}. Razón: ${command.reason}`
}
