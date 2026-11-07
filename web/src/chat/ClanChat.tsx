import ChatBubbleOutlineIcon from '@mui/icons-material/ChatBubbleOutline'
import CloseIcon from '@mui/icons-material/Close'
import GpsFixedIcon from '@mui/icons-material/GpsFixed'
import ImageOutlinedIcon from '@mui/icons-material/ImageOutlined'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import SendIcon from '@mui/icons-material/Send'
import { Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup, Tooltip, Typography } from '@mui/material'
import { createClient, type RealtimeChannel } from '@supabase/supabase-js'
import { useEffect, useRef, useState, type ClipboardEvent } from 'react'
import { api, post } from '../api'
import { useUi } from '../ui'

const supabase = createClient('https://uyzkcejpsjpavghbchoq.supabase.co', 'sb_publishable_Wdj9c0igv_Th0TNGqgN2Uw_TaA-ruCN')

const emojis = ['😀', '😁', '😂', '😉', '😎', '😭', '👍', '🙏', '🔥', '💪', '🎉', '✅', '⚔️', '🛡️', '🏰', '🌾', '🐎', '🏹']
const imageTypes = new Set(['image/jpeg', 'image/png', 'image/webp', 'image/gif'])

function imageFromClipboard(data: DataTransfer | null) {
  if (!data) return null
  for (const item of data.items) {
    if (item.kind !== 'file' || !item.type.startsWith('image/')) continue
    const blob = item.getAsFile()
    if (!blob) continue
    const type = blob.type || item.type
    const extension = type === 'image/jpeg' ? 'jpg' : (type.split('/')[1] || 'png')
    const name = blob.name && !/^image\./i.test(blob.name) ? blob.name : `Imagen pegada.${extension}`
    return new File([blob], name, { type })
  }
  return null
}

const announced = new Set<string>()
let sharedAudio: AudioContext | null = null
let lastChime = 0
let chimePending = false

function chimeContext() {
  const Context = window.AudioContext ?? (window as Window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!Context) return null
  sharedAudio ??= new Context()
  return sharedAudio
}

function tone(ctx: AudioContext, master: GainNode, freq: number, start: number, duration: number, peak: number) {
  const osc = ctx.createOscillator()
  const gain = ctx.createGain()
  osc.type = 'sine'
  osc.frequency.setValueAtTime(freq, start)
  osc.frequency.exponentialRampToValueAtTime(freq * 0.99, start + duration)
  gain.gain.setValueAtTime(0.0001, start)
  gain.gain.exponentialRampToValueAtTime(peak, start + 0.018)
  gain.gain.exponentialRampToValueAtTime(0.0001, start + duration)
  osc.connect(gain)
  gain.connect(master)
  osc.start(start)
  osc.stop(start + duration + 0.02)
}

function ring(ctx: AudioContext) {
  const master = ctx.createGain()
  master.gain.value = 0.2
  master.connect(ctx.destination)
  const now = ctx.currentTime
  tone(ctx, master, 784, now, 0.32, 0.4)
  tone(ctx, master, 1174.66, now + 0.1, 0.42, 0.32)
  tone(ctx, master, 1567.98, now + 0.1, 0.24, 0.08)
}

function playIncoming() {
  const now = Date.now()
  if (chimePending || now - lastChime < 900) return
  const ctx = chimeContext()
  if (!ctx) return
  chimePending = true
  const start = () => {
    chimePending = false
    if (ctx.state !== 'running') return
    lastChime = Date.now()
    ring(ctx)
  }
  if (ctx.state === 'suspended') void ctx.resume().then(start).catch(() => { chimePending = false })
  else start()
}

function unlockChime() {
  const ctx = chimeContext()
  if (ctx?.state === 'suspended') void ctx.resume()
}

window.addEventListener('pointerdown', unlockChime)

const dayMs = 24 * 60 * 60 * 1000

type ChatMessage = {
  id: string
  room: string
  username: string
  body: string
  image_path: string | null
  created_at: string
}

type MessageRead = {
  room: string
  username: string
  read_at: string
}

function isFresh(message: ChatMessage, now: number) {
  return (Date.parse(message.created_at) || 0) > now - dayMs
}

function seenBy(message: ChatMessage, reads: MessageRead[]) {
  const at = Date.parse(message.created_at) || 0
  const author = (message.username ?? '').trim()
  const names = new Set<string>()
  for (const read of reads) {
    const name = (read.username ?? '').trim()
    if (!name || name === author) continue
    if ((Date.parse(read.read_at) || 0) < at) continue
    names.add(name)
  }
  return [...names].sort((left, right) => left.localeCompare(right, 'es'))
}

async function purgeOld() {
  await supabase.rpc('purge_old_chat')
}

function roomOf(host: string, clan: string) {
  return `${host.trim().toLowerCase()}|${clan.trim()}`
}

function imageUrl(path: string) {
  const encoded = path.split('/').map(part => encodeURIComponent(part)).join('/')
  return `https://uyzkcejpsjpavghbchoq.supabase.co/storage/v1/object/public/chat-images/${encoded}`
}

type ChatPart = { kind: 'text' | 'link'; value: string; x: number | null; y: number | null }
type OwnVillage = { id: number; name: string; x: number; y: number }
type HomeTroop = { letter: string; index: number; name: string; available: number }
type AttackTarget = { url: string; x: number; y: number }

function villageOf(url: string) {
  try {
    const parsed = new URL(url)
    if (!/\/karte\.php$/i.test(parsed.pathname)) return null
    const x = parsed.searchParams.get('x')
    const y = parsed.searchParams.get('y')
    if (x == null || y == null || !/^-?\d+$/.test(x) || !/^-?\d+$/.test(y)) return null
    return { x: Number(x), y: Number(y) }
  } catch {
    return null
  }
}

function messageParts(text: string): ChatPart[] {
  const linkPattern = /https?:\/\/[^\s<>"']+/gi
  const parts: ChatPart[] = []
  let last = 0
  for (const match of text.matchAll(linkPattern)) {
    const start = match.index ?? 0
    if (start > last) parts.push({ kind: 'text', value: text.slice(last, start), x: null, y: null })
    let href = match[0]
    let trailing = ''
    while (/[.,;:!?)\]]$/.test(href)) {
      trailing = href.slice(-1) + trailing
      href = href.slice(0, -1)
    }
    const village = href ? villageOf(href) : null
    if (href) parts.push({ kind: 'link', value: href, x: village?.x ?? null, y: village?.y ?? null })
    if (trailing) parts.push({ kind: 'text', value: trailing, x: null, y: null })
    last = start + match[0].length
  }
  if (last < text.length) parts.push({ kind: 'text', value: text.slice(last), x: null, y: null })
  return parts
}

function ChatLink({ part, onAttack }: { part: ChatPart; onAttack: (target: AttackTarget) => void }) {
  const village = part.x != null && part.y != null
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.25, maxWidth: '100%', verticalAlign: 'middle' }}>
      {village && (
        <Tooltip title="Atacar">
          <IconButton size="small" aria-label={`Atacar Aldea (${part.x}|${part.y})`} onClick={() => onAttack({ url: part.value, x: part.x as number, y: part.y as number })} sx={{ color: '#a86f08', p: 0.25 }}>
            <GpsFixedIcon sx={{ fontSize: 16 }} />
          </IconButton>
        </Tooltip>
      )}
      <Tooltip title="Abrir en Chrome">
        <Box component="a" href={part.value} target="_blank" rel="noreferrer" sx={{ color: '#1f5e9c', fontWeight: 600, textDecoration: 'underline', display: 'inline-flex', alignItems: 'center', gap: 0.25, minWidth: 0, wordBreak: 'break-all', textUnderlineOffset: '2px', '&:hover': { textDecoration: 'underline' } }}>
          {village ? `Aldea (${part.x}|${part.y})` : part.value}
          <OpenInNewIcon sx={{ fontSize: 12, flex: 'none' }} />
        </Box>
      </Tooltip>
    </Box>
  )
}

const helpText = [
  '/stop Nombre razón',
  'Pausa todo el bot de esa persona.',
  '',
  '/stop farming Nombre razón',
  'Detiene el farming: listas propias, listas del juego y ataques en cola.',
  '',
  '/stop building Nombre razón',
  'Detiene la construcción de las aldeas. aldeas es lo mismo que building.',
  '',
  'La razón es obligatoria. /help y /stop solos muestran esta ayuda.',
].join('\n')

type ClanCommand =
  | { kind: 'help' }
  | { kind: 'need-reason' }
  | { kind: 'stop'; feature: 'all' | 'farming' | 'building'; target: string; reason: string }

function parseClanCommand(body: string): ClanCommand | null {
  const parts = body.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return null
  const command = parts[0].toLowerCase()
  if (command === '/help') return parts.length === 1 ? { kind: 'help' } : null
  if (command !== '/stop') return null
  if (parts.length === 1) return { kind: 'help' }
  let index = 1
  let feature: 'all' | 'farming' | 'building' = 'all'
  const word = parts[1].toLowerCase()
  if (word === 'farming' || word === 'building' || word === 'aldeas') {
    feature = word === 'farming' ? 'farming' : 'building'
    index = 2
  }
  if (parts.length < index + 2) return { kind: 'need-reason' }
  return { kind: 'stop', feature, target: parts[index], reason: parts.slice(index + 1).join(' ') }
}

function stopLabel(feature: 'all' | 'farming' | 'building') {
  if (feature === 'farming') return 'el farming'
  if (feature === 'building') return 'la construcción'
  return 'el bot'
}

function stamp(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' })
}

function readMark(room: string) {
  const stored = Number(localStorage.getItem(`clan-read:${room}`) || 0)
  return Number.isFinite(stored) ? stored : 0
}

export function ClanChat({ username, host, clan, page, accountId }: { username: string; host: string; clan: string; page: string; accountId: number }) {
  const ui = useUi()
  const room = roomOf(host, clan)
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [reads, setReads] = useState<MessageRead[]>([])
  const [people, setPeople] = useState<string[]>([])
  const [now, setNow] = useState(() => Date.now())
  const [draft, setDraft] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [previewUrl, setPreviewUrl] = useState('')
  const [emojisOpen, setEmojisOpen] = useState(false)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState('')
  const [attack, setAttack] = useState<AttackTarget | null>(null)
  const [villages, setVillages] = useState<OwnVillage[]>([])
  const [fromId, setFromId] = useState(0)
  const [troops, setTroops] = useState('')
  const [raid, setRaid] = useState(true)
  const [home, setHome] = useState<HomeTroop[] | null>(null)
  const [homeError, setHomeError] = useState('')
  const [attacking, setAttacking] = useState(false)
  const [notice, setNotice] = useState<{ title: string; text: string } | null>(null)
  const handled = useRef(new Set<string>())
  const [readAt, setReadAt] = useState(() => readMark(room))
  const pageRef = useRef(page)
  const listRef = useRef<HTMLDivElement | null>(null)
  const sharedRead = useRef(0)

  useEffect(() => {
    if (pageRef.current !== page) setOpen(false)
    pageRef.current = page
  }, [page])

  useEffect(() => {
    const clock = window.setInterval(() => setNow(Date.now()), 60_000)
    const cleaner = window.setInterval(() => { void purgeOld() }, 10 * 60 * 1000)
    void purgeOld()
    return () => {
      window.clearInterval(clock)
      window.clearInterval(cleaner)
    }
  }, [])

  useEffect(() => {
    let alive = true
    const channel: RealtimeChannel = supabase.channel(`clan:${room}`, { config: { presence: { key: username } } })
    channel
      .on('postgres_changes', { event: 'INSERT', schema: 'public', table: 'messages' }, payload => {
        const message = payload.new as ChatMessage
        if (message.room !== room) return
        if (!isFresh(message, Date.now())) return
        setMessages(current => current.some(item => item.id === message.id) ? current : [...current, message])
        const body = message.body ?? ''
        const author = (message.username ?? '').trim()
        const joinNotice = body.trimEnd().endsWith(' se ha conectado.')
        if (author !== username.trim() && !joinNotice) playIncoming()
        const command = parseClanCommand(body)
        if (command && message.id && !handled.current.has(message.id)) {
          handled.current.add(message.id)
          const mine = author.localeCompare(username.trim(), 'es', { sensitivity: 'accent' }) === 0
          if (command.kind === 'help' && mine) setNotice({ title: 'Comandos', text: helpText })
          if (command.kind === 'need-reason' && mine) setNotice({ title: 'Falta la razón', text: `La razón es obligatoria.\n\n${helpText}` })
          if (command.kind === 'stop' && command.target.localeCompare(username.trim(), 'es', { sensitivity: 'accent' }) === 0) {
            const label = stopLabel(command.feature)
            const text = `${author} te detuvo ${label} por: ${command.reason}`
            setNotice({ title: 'Bot detenido', text })
            void post(`/api/accounts/${accountId}/remote-stop`, { feature: command.feature }).catch((reason: unknown) => {
              setNotice({ title: 'Bot detenido', text: `${text}\n\n${reason instanceof Error ? reason.message : 'No se pudo completar la detención.'}` })
            })
          }
        }
      })
      .on('postgres_changes', { event: 'DELETE', schema: 'public', table: 'messages' }, payload => {
        const id = (payload.old as { id?: string }).id
        if (!id) return
        setMessages(current => current.filter(item => item.id !== id))
      })
      .on('postgres_changes', { event: '*', schema: 'public', table: 'message_reads' }, payload => {
        if (payload.eventType === 'DELETE') {
          const read = payload.old as MessageRead
          if (read.room && read.room !== room) return
          setReads(current => current.filter(item => item.username !== read.username))
          return
        }
        const read = payload.new as MessageRead
        if (!read || read.room !== room) return
        setReads(current => [...current.filter(item => item.username !== read.username), read])
      })
      .on('presence', { event: 'sync' }, () => {
        const names = new Set<string>()
        for (const entries of Object.values(channel.presenceState<{ username?: string }>())) {
          for (const entry of entries) if (entry.username) names.add(entry.username)
        }
        setPeople([...names].sort((left, right) => left.localeCompare(right, 'es')))
      })
      .subscribe(status => {
        if (status === 'SUBSCRIBED') void channel.track({ username })
      })

    const since = new Date(Date.now() - dayMs).toISOString()
    supabase.from('messages').select('id,room,username,body,image_path,created_at').eq('room', room).gte('created_at', since).order('created_at', { ascending: false }).limit(80)
      .then(({ data }) => {
        if (!alive || !data) return
        setMessages([...data].reverse() as ChatMessage[])
      })
    supabase.from('message_reads').select('room,username,read_at').eq('room', room)
      .then(({ data }) => {
        if (!alive || !data) return
        setReads(data as MessageRead[])
      })

    const key = `${room}:${username}`
    if (!announced.has(key) && !sessionStorage.getItem(`clan-joined:${key}`)) {
      announced.add(key)
      void supabase.from('messages').insert({ room, username, body: `${username} se ha conectado.`, image_path: null }).then(({ error: insertError }) => {
        if (insertError) announced.delete(key)
        else sessionStorage.setItem(`clan-joined:${key}`, '1')
      })
    }

    return () => {
      alive = false
      void supabase.removeChannel(channel)
    }
  }, [room, username, accountId])

  useEffect(() => {
    sharedRead.current = 0
  }, [room, username])

  useEffect(() => {
    if (!open) return
    const latest = messages.reduce((max, message) => Math.max(max, Date.parse(message.created_at) || 0), 0)
    if (latest > readAt) {
      localStorage.setItem(`clan-read:${room}`, String(latest))
      setReadAt(latest)
    }
    if (latest > 0 && latest !== sharedRead.current) {
      sharedRead.current = latest
      void supabase.from('message_reads').upsert(
        { room, username, read_at: new Date(latest).toISOString() },
        { onConflict: 'room,username' },
      ).then(({ error: upsertError }) => {
        if (upsertError) sharedRead.current = 0
      })
    }
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight })
  }, [open, messages, readAt, room, username])

  useEffect(() => {
    if (!file) {
      setPreviewUrl('')
      return
    }
    const url = URL.createObjectURL(file)
    setPreviewUrl(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  function pasteImage(event: ClipboardEvent) {
    const image = imageFromClipboard(event.clipboardData)
    if (!image) return
    event.preventDefault()
    if (!imageTypes.has(image.type) || image.size > 1.5 * 1024 * 1024) {
      setError('La imagen tiene que ser jpg, png, webp o gif, de hasta 1,5 MB.')
      return
    }
    setError('')
    setFile(image)
  }

  const visible = messages.filter(message => isFresh(message, now))
  const unread = open ? 0 : visible.filter(message => message.username !== username && (Date.parse(message.created_at) || 0) > readAt).length

  async function send() {
    const text = draft.trim()
    if ((!text && !file) || sending) return
    if (file && (!imageTypes.has(file.type) || file.size > 1.5 * 1024 * 1024)) {
      setError('La imagen tiene que ser jpg, png, webp o gif, de hasta 1,5 MB.')
      return
    }
    setSending(true)
    setError('')
    let imagePath: string | null = null
    if (file) {
      const extension = file.type === 'image/jpeg' ? 'jpg' : file.type.slice('image/'.length)
      imagePath = `${crypto.randomUUID()}.${extension}`
      const uploaded = await supabase.storage.from('chat-images').upload(imagePath, file, { contentType: file.type, upsert: false })
      if (uploaded.error) {
        setSending(false)
        setError('No se pudo enviar la imagen.')
        return
      }
    }
    const inserted = await supabase.from('messages').insert({ room, username, body: text, image_path: imagePath })
    setSending(false)
    if (inserted.error) {
      setError('No se pudo enviar.')
      return
    }
    setDraft('')
    setFile(null)
    setEmojisOpen(false)
  }

  useEffect(() => {
    if (!attack || !fromId) return
    let cancel = false
    setHome(null)
    setHomeError('')
    api<HomeTroop[]>(`/api/accounts/${accountId}/villages/${fromId}/home-troops`)
      .then(result => { if (!cancel) setHome(result.data) })
      .catch((reason: unknown) => {
        if (cancel) return
        setHome([])
        setHomeError(reason instanceof Error ? reason.message : 'No se pudieron leer las tropas')
      })
    return () => { cancel = true }
  }, [attack, fromId, accountId])

  async function openAttack(target: AttackTarget) {
    setAttack(target)
    setTroops('')
    setRaid(true)
    setHome(null)
    setHomeError('')
    try {
      const result = await api<OwnVillage[]>(`/api/accounts/${accountId}/villages`)
      setVillages(result.data)
      setFromId(current => result.data.some(village => village.id === current) ? current : (result.data[0]?.id ?? 0))
    } catch (reason) {
      setVillages([])
      setHomeError(reason instanceof Error ? reason.message : 'No se pudieron leer las aldeas')
    }
  }

  function addToPlan(troop: HomeTroop) {
    const parts = troops.trim().split(/\s+/).filter(Boolean).filter(part => !part.toUpperCase().startsWith(`${troop.letter}:`))
    parts.push(`${troop.letter}:${troop.available}`)
    setTroops(parts.join(' '))
  }

  async function sendAttack() {
    if (!attack || !fromId || attacking) return
    setAttacking(true)
    try {
      const result = await post(`/api/accounts/${accountId}/chat/attack`, { url: attack.url, villageId: fromId, troops: troops.trim() || 'A:50', raid })
      ui.notify(result.message)
      setAttack(null)
    } catch (reason) {
      ui.notify(reason instanceof Error ? reason.message : 'No se pudo atacar')
    } finally {
      setAttacking(false)
    }
  }

  const fromVillage = villages.find(village => village.id === fromId)

  return (
    <Box sx={{ position: 'fixed', right: 16, bottom: 16, zIndex: 20, display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 1 }}>
      {open && (
        <Box onPasteCapture={pasteImage} sx={{ width: 'min(340px, calc(100vw - 32px))', height: 'min(480px, calc(100vh - 96px))', display: 'flex', flexDirection: 'column', bgcolor: '#fbf5e2', borderRadius: 2, boxShadow: '0 12px 40px rgba(0,0,0,.45)', overflow: 'hidden' }}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1.5, py: 1, bgcolor: '#e8dab3' }}>
            <Box sx={{ flex: 1, minWidth: 0 }}>
              <Typography noWrap sx={{ fontWeight: 700 }}>{clan}</Typography>
              <Typography noWrap variant="caption" color="text.secondary">{people.length ? people.join(', ') : 'Conectando'}</Typography>
            </Box>
            <IconButton size="small" aria-label="Cerrar chat" onClick={() => setOpen(false)} sx={{ color: '#4a3820' }}>
              <CloseIcon fontSize="small" />
            </IconButton>
          </Box>
          <Box ref={listRef} sx={{ flex: 1, overflow: 'auto', px: 1.25, py: 1, display: 'flex', flexDirection: 'column', gap: 0.75 }}>
            {visible.map(message => {
              const mine = message.username === username
              const seen = seenBy(message, reads)
              return (
                <Box key={message.id} sx={{ alignSelf: mine ? 'flex-end' : 'flex-start', maxWidth: '85%', bgcolor: mine ? '#e4efcc' : '#fffaf0', color: '#2e2010', border: '1px solid', borderColor: mine ? '#a9c27a' : '#d6c296', boxShadow: '0 1px 2px rgba(91,58,27,.15)', borderRadius: 1.5, px: 1.1, py: 0.7 }}>
                  <Typography variant="caption" sx={{ color: mine ? '#3f6f0a' : '#8a4b12', fontWeight: 700 }}>{message.username}</Typography>
                  {message.body && (
                    <Typography sx={{ fontSize: 14, color: '#2e2010', whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                      {messageParts(message.body).map((part, index) => part.kind === 'link' ? (
                        <ChatLink key={index} part={part} onAttack={openAttack} />
                      ) : (
                        <span key={index}>{part.value}</span>
                      ))}
                    </Typography>
                  )}
                  {message.image_path && <Box component="img" src={imageUrl(message.image_path)} alt="" sx={{ display: 'block', mt: 0.5, maxWidth: '100%', borderRadius: 1 }} />}
                  <Typography variant="caption" color="text.secondary">{stamp(message.created_at)}</Typography>
                  {seen.length > 0 && (
                    <Typography variant="caption" title={seen.join(', ')} sx={{ display: 'block', color: '#3f6f0a', lineHeight: 1.3 }}>
                      Visto por {seen.join(', ')}
                    </Typography>
                  )}
                </Box>
              )
            })}
          </Box>
          {emojisOpen && (
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, px: 1.25, pb: 0.5 }}>
              {emojis.map(emoji => (
                <Box key={emoji} component="button" type="button" onClick={() => setDraft(current => `${current}${emoji}`)} sx={{ border: 0, bgcolor: 'transparent', cursor: 'pointer', fontSize: 18, lineHeight: 1, p: 0.25 }}>
                  {emoji}
                </Box>
              ))}
            </Box>
          )}
          {file && (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 1.5, minWidth: 0 }}>
              {previewUrl && <Box component="img" src={previewUrl} alt="" sx={{ width: 36, height: 36, objectFit: 'cover', borderRadius: 0.5, flex: 'none' }} />}
              <Typography noWrap variant="caption" color="text.secondary">{file.name}</Typography>
            </Box>
          )}
          {error && <Typography variant="caption" color="error" sx={{ px: 1.5 }}>{error}</Typography>}
          <Box sx={{ display: 'flex', alignItems: 'flex-end', gap: 0.5, p: 1 }}>
            <IconButton size="small" aria-label="Iconos" onClick={() => setEmojisOpen(current => !current)} sx={{ color: '#4a3820' }}>
              <Typography sx={{ fontSize: 16, lineHeight: 1 }}>☺</Typography>
            </IconButton>
            <IconButton size="small" component="label" aria-label="Imagen" sx={{ color: '#4a3820' }}>
              <ImageOutlinedIcon fontSize="small" />
              <input hidden type="file" accept="image/jpeg,image/png,image/webp,image/gif" onChange={event => { setFile(event.target.files?.[0] ?? null); event.target.value = '' }} />
            </IconButton>
            <TextField size="small" fullWidth placeholder="Escribí un mensaje" value={draft} onChange={event => setDraft(event.target.value)} onKeyDown={event => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void send() } }} />
            <IconButton size="small" aria-label="Enviar" disabled={sending || (!draft.trim() && !file)} onClick={() => { void send() }} sx={{ color: '#4f8a10' }}>
              <SendIcon fontSize="small" />
            </IconButton>
          </Box>
        </Box>
      )}
      <Box
        component="button"
        type="button"
        onClick={() => setOpen(current => !current)}
        sx={{ position: 'relative', border: 0, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 0.75, bgcolor: '#4f8a10', color: '#fff', borderRadius: 999, px: 1.5, py: 1, fontWeight: 700, fontSize: 13, boxShadow: '0 8px 24px rgba(0,0,0,.35)' }}
      >
        <ChatBubbleOutlineIcon sx={{ fontSize: 18 }} />
        {clan}
        {unread > 0 && (
          <Box component="span" sx={{ position: 'absolute', top: -6, right: -4, minWidth: 18, height: 18, px: 0.5, borderRadius: 999, bgcolor: '#b8281d', color: '#fff', fontSize: 13.5, lineHeight: '18px', textAlign: 'center' }}>
            {unread > 99 ? '99+' : unread}
          </Box>
        )}
      </Box>
      <Dialog open={notice != null} onClose={() => setNotice(null)} fullWidth maxWidth="xs">
        <DialogTitle>{notice?.title}</DialogTitle>
        <DialogContent>
          <Typography sx={{ whiteSpace: 'pre-line' }}>{notice?.text}</Typography>
        </DialogContent>
        <DialogActions>
          <Button variant="contained" onClick={() => setNotice(null)}>Entendido</Button>
        </DialogActions>
      </Dialog>
      <Dialog open={attack != null} onClose={() => !attacking && setAttack(null)} fullWidth maxWidth="sm">
        <DialogTitle>Atacar {attack ? `Aldea (${attack.x}|${attack.y})` : ''}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField select label="Las tropas salen de" value={fromId || ''} onChange={event => setFromId(Number(event.target.value))} disabled={villages.length === 0}>
              {villages.map(village => <MenuItem key={village.id} value={village.id}>{village.name} ({village.x}|{village.y})</MenuItem>)}
            </TextField>
            <Box>
              <Typography variant="caption" color="text.secondary">Tus tropas en {fromVillage?.name ?? 'la aldea'} (click para mandar todas)</Typography>
              {attack && fromId > 0 && home == null && !homeError && <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 1 }}><CircularProgress size={16} /><Typography variant="body2">Leyendo el punto de reunión...</Typography></Stack>}
              {homeError && <Typography variant="body2" color="error" sx={{ mt: 1 }}>{homeError}</Typography>}
              {home && home.length > 0 && (
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75, mt: 1 }}>
                  {home.map(troop => (
                    <Chip key={troop.letter} label={`${troop.letter} · ${troop.name}: ${troop.available}`} disabled={troop.available === 0} onClick={() => addToPlan(troop)} variant={troop.available > 0 ? 'filled' : 'outlined'} sx={{ bgcolor: troop.available > 0 ? '#404249' : 'transparent' }} />
                  ))}
                </Box>
              )}
            </Box>
            <TextField label="Tropas" placeholder="A:50" helperText="Letra de la casilla y cantidad, por ejemplo A:50 o A:30 D:2. Vacío manda A:50." value={troops} onChange={event => setTroops(event.target.value)} />
            <ToggleButtonGroup exclusive size="small" value={raid ? 'raid' : 'normal'} onChange={(_, value) => value && setRaid(value === 'raid')}>
              <ToggleButton value="raid">Asalto</ToggleButton>
              <ToggleButton value="normal">Ataque normal</ToggleButton>
            </ToggleButtonGroup>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAttack(null)} disabled={attacking}>Cancelar</Button>
          <Button variant="contained" color="warning" startIcon={<GpsFixedIcon />} disabled={!fromId || attacking} onClick={() => { void sendAttack() }}>Atacar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  )
}
